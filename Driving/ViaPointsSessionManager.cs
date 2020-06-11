using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using MetroLog;
using Company.System.Driving;
using Company.App.Services.Contracts.Analytics;
using Company.App.Services.Contracts.Guidance.Driving;
using Company.App.Services.Contracts.Location;
using Company.App.Services.Contracts.Logging;
using Company.App.Services.Contracts.Models.GeoObject;
using Company.App.Services.Contracts.Models.MapObjects;
using Company.App.Services.Contracts.Models.Route.Driving;
using Company.App.Services.Contracts.Routing;
using Company.App.Services.Contracts.Search;
using Company.App.Services.Extensions;
using Company.App.Services.Map.Models.Route;
using Company.App.Services.Routing;
using Company.App.Utils.Logging;
using Unit = System.Reactive.Unit;
using ViaPointsUnion =
    Company.App.Utils.Union.Union3<Company.App.Services.Guidance.Driving.IViaPointData, Company.App.Services.Guidance.Driving.IViaPointData, System.Reactive.Unit>;

namespace Company.App.Services.Guidance.Driving
{
    internal partial class ViaPointsSessionManager : IViaPointsSessionManager, IDisposable
    {
        #region Static and Readonly Fields

        private readonly Subject<(IViaPointData ViaPointData, Route Route)> _addViaPointSubject = new Subject<(IViaPointData ViaPointData, Route RouteAfterAdding)>();

        private readonly Subject<(IViaPointData ViaPointData, Route Route)> _deleteViaPointSubject = new Subject<(IViaPointData ViaPointData, Route RouteAfterDeleting)>();

        private readonly Subject<IViaPointError> _errorSubject = new Subject<IViaPointError>();

        private readonly IInternalRouteService _internalRouteService;
        private readonly ILocationService _locationService;
        private readonly ILogger _logger;
        private readonly IRouteTimeService _routeTimeService;
        private readonly ISearchService _searchService;

        private readonly IDisposable _subscribes;

        #endregion

        #region Fields

        private DrivingRouteObject _currentRoute;
        private IViaPointData[] _viaPoints = new IViaPointData[0];

        #endregion

        #region Constructors

        public ViaPointsSessionManager(ILogManager logManager,
            IInternalRouteService internalRouteService,
            ILocationService locationService,
            ISearchService searchService,
            IRouteTimeService routeTimeService,
            IObservable<DrivingRouteObject> currentRouteStream)
        {
            _logger = logManager.GetLogger(Scopes.Driving, LogManagerFactory.DefaultConfiguration);
            _internalRouteService = internalRouteService;
            _locationService = locationService;
            _searchService = searchService;
            _routeTimeService = routeTimeService;

            IObservable<ViaPointsUnion> addStream = _addViaPointSubject.Select(add => new ViaPointsUnion.Case1(add.ViaPointData)).Do(_ => _logger.Debug("Via point ADDED"));
            IObservable<ViaPointsUnion> delStream = _deleteViaPointSubject.Select(del => new ViaPointsUnion.Case2(del.ViaPointData)).Do(_ => _logger.Debug("Via point DELETED"));
            IObservable<ViaPointsUnion> passStream = currentRouteStream.Select(r => r.ViaPointPassed)
                .Switch()
                .Select(_ => new ViaPointsUnion.Case3(_))
                .Do(_ => _logger.Debug("Via point PASSED"));
            IObservable<IEnumerable<IViaPointData>> viaPointsStream = addStream.Merge(delStream).Merge(passStream).Scan(Enumerable.Empty<IViaPointData>(), ViaPointsAccumulator);

            ViaPointTapped = currentRouteStream.Select(r => r.ViaPointTapped).Switch().Select(GetTappedViaPoint).Publish().RefCount();
            ChangedRouteStream = _addViaPointSubject.Merge(_deleteViaPointSubject).Select(t => t.Route).Publish().RefCount();
            ViaPointPassed = currentRouteStream.Select(r => r.ViaPointPassed).Switch().Publish().RefCount();

            _subscribes = new CompositeDisposable(currentRouteStream.Subscribe(r => _currentRoute = r), viaPointsStream.Subscribe(viaPoints => _viaPoints = viaPoints.ToArray()));
        }

        #endregion

        #region Properties

        public IObservable<Route> ChangedRouteStream { get; }

        #endregion

        #region Static Methods

        private static IEnumerable<IViaPointData> ViaPointsAccumulator(IEnumerable<IViaPointData> viaPoints, ViaPointsUnion union)
        {
            return union.Match(add => viaPoints.Concat(new[] { add }), del => viaPoints.Except(new[] { del }), _ => viaPoints.Skip(1));
        }

        #endregion

        #region Methods

        private async Task<Option<Route>> GetRouteWithPoint(Coordinate coordinate)
        {
            _logger.Method().Start(coordinate);

            if (!_locationService.LastPosition.HasValue)
                return Option<Route>.None;

            List<Coordinate> viaPoints = _viaPoints.Select(vp => vp.UserObjectCoordinate).ToList();
            viaPoints.Add(coordinate);
            _logger.Info($"via points count became {viaPoints.Count}");

            IReadOnlyList<Route> routes = await _internalRouteService.GetDrivingRoutes(_locationService.LastPosition.Value,
                _currentRoute.Route.Geometry.Points.Last().ToCoordinate(),
                viaPoints,
                CancellationToken.None);

            Option<Route> route = Prelude.Optional(routes.FirstOrDefault());

            if (route.IsNone)
            {
                Debug.Assert(false);
                _logger.Method(LogLevel.Warn).End("no routes found with added via point");
            }

            return route.RetWithLog(_logger).Log();
        }

        private ViaPointPoi GetTappedViaPoint(ViaPointObject viaPointObject)
        {
            int index = _currentRoute.ViaPoints.ToList().IndexOf(viaPointObject);
            IViaPointData p = _viaPoints[index];
            var viaPointPoi = new ViaPointPoi(p.Uri,
                () =>
                {
                    viaPointObject.IsSelected = true;
                    return Task.CompletedTask;
                },
                () => viaPointObject.IsSelected = false);
            return viaPointPoi;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _subscribes.Dispose();
        }

        #endregion

        #region IViaPointsSessionManager Members

        public IObservable<IViaPointError> Error => _errorSubject;

        public IObservable<Unit> ViaPointPassed { get; }

        public IReadOnlyList<Coordinate> ViaPointsCoordinates => _viaPoints.Select(vp => vp.UserObjectCoordinate).ToList();

        public IObservable<ViaPointPoi> ViaPointTapped { get; }

        public async Task AddViaPoint(Coordinate coordinate)
        {
            string routeIdBeforeAdding = _currentRoute.Route.RouteId;

            Option<Route> optRoute = await GetRouteWithPoint(coordinate);

            Either<IViaPointError, (IViaPointData ViaPointData, Route Route)> res = await optRoute.MatchAsync(async route =>
                {
                    try
                    {
                        IToponymObjectModel obj = await _searchService.WhatHere(coordinate);
                        var viaPoint = new ViaPointToponymData(coordinate, obj.GeometryPosition, obj.Uri);
                        return Prelude.Right<IViaPointError, (IViaPointData ViaPointData, Route Route)>((viaPoint, route));
                    }
                    catch (Exception ex)
                    {
                        ExceptionHandler.FailInDebugTrackInRelease(ex);
                        return new ViaPointError(() => AddViaPoint(coordinate));
                    }
                },
                () => new ViaPointError(() => AddViaPoint(coordinate)));

            if (routeIdBeforeAdding != _currentRoute.Route.RouteId)
            {
                await AddViaPoint(coordinate);
                return;
            }

            res.Match(_addViaPointSubject.OnNext, _errorSubject.OnNext);
        }

        public async Task AddViaPoint(IPoiModel poiModel)
        {
            string routeIdBeforeAdding = _currentRoute.Route.RouteId;

            var visitor = new PoiVisitor<IViaPointData>(model => new ViaPointToponymData(model.Position, model.GeometryPosition, model.Uri),
                model => new ViaPointBusinessOrStopData(model.Position, model.Uri),
                stop => new ViaPointBusinessOrStopData(stop.Position, stop.Uri));
            poiModel.AcceptVisitor(visitor);

            IViaPointData viaPointData = visitor.Result;

            Option<Route> optRoute = await GetRouteWithPoint(viaPointData.UserObjectCoordinate);

            Either<IViaPointError, (IViaPointData ViaPointData, Route Route)> res =
                optRoute.Match(route => Prelude.Right<IViaPointError, (IViaPointData ViaPointData, Route Route)>((viaPointData, route)),
                    () => new ViaPointError(() => AddViaPoint(poiModel)));

            if (routeIdBeforeAdding != _currentRoute.Route.RouteId)
            {
                await AddViaPoint(poiModel);
                return;
            }

            res.Match(_addViaPointSubject.OnNext, _errorSubject.OnNext);
        }

        public async Task DeleteViaPoint(IPoiModel poiModel)
        {
            if (!_locationService.LastPosition.HasValue)
            {
                _errorSubject.OnNext(new ViaPointError(() => DeleteViaPoint(poiModel)));
                return;
            }

            string routeIdBeforeAdding = _currentRoute.Route.RouteId;

            IViaPointData viaPointData = _viaPoints.First(p => p.IsSameAs(poiModel));

            int index = Array.IndexOf(_viaPoints, viaPointData);
            ViaPointObject viaPointObject = _currentRoute.ViaPoints[index];
            viaPointObject.IsSelected = false;
            IViaPointData[] restViaPoints = _viaPoints.Except(new[] { viaPointData }).ToArray();
            Coordinate[] newViaPointsCoordinates = restViaPoints.Select(vp => vp.UserObjectCoordinate).ToArray();

            IReadOnlyList<Route> routes = await _internalRouteService.GetDrivingRoutes(_locationService.LastPosition.Value,
                _currentRoute.Route.Geometry.Points.Last().ToCoordinate(),
                newViaPointsCoordinates,
                CancellationToken.None);

            Route route = routes.FirstOrDefault();
            if (route == null)
            {
                _logger.Method(LogLevel.Warn).End("no routes found with added via point");
                _errorSubject.OnNext(new ViaPointError(() => DeleteViaPoint(poiModel)));
                return;
            }

            if (routeIdBeforeAdding != _currentRoute.Route.RouteId)
            {
                await DeleteViaPoint(poiModel);
                return;
            }

            _deleteViaPointSubject.OnNext((viaPointData, route));
        }

        public async Task<double?> GetTimeDiffWithViaPoint(Coordinate coordinate, CancellationToken cancellationToken)
        {
            List<Coordinate> viaPoints = _viaPoints.Select(vp => vp.UserObjectCoordinate).ToList();

            viaPoints.Add(coordinate);
            DrivingTime newRoutetime = await _routeTimeService.GetDrivingRouteTime(_currentRoute.Route.Geometry.Points.Last().ToCoordinate(), viaPoints, cancellationToken);
            if (newRoutetime == null)
                return null;
            Weight restRouteWeight = _currentRoute.Route.Metadata.Weight;

            return newRoutetime.Time.Value - restRouteWeight.TimeWithTraffic.Value;
        }

        public bool IsPointAlreadyOnRoute(IPoiModel poiModel) => _viaPoints.Any(p => p.IsSameAs(poiModel));

        #endregion
    }
}
