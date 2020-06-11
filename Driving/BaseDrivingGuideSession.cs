using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MetroLog;
using Company.System.Driving;
using Company.System.Geometry;
using Company.System.Location;
using Company.System.Map;
using Company.App.Services.Contracts.Guidance.Driving;
using Company.App.Services.Contracts.Location;
using Company.App.Services.Contracts.Logging;
using Company.App.Services.Contracts.Map.Presenters;
using Company.App.Services.Contracts.UserData;
using Company.App.Services.Extensions;
using Company.App.Services.Location;
using Company.App.Services.Map.Models.Route;
using Company.App.Services.Map.Presenters;
using Company.App.Services.Map.Renderers;
using Company.App.Utils;
using Company.App.Utils.Extensions;

namespace Company.App.Services.Guidance.Driving
{
    internal abstract partial class BaseDrivingGuideSession : IDrivingGuideSession, ISimpleDrivingSessionStreams
    {
        #region Static and Readonly Fields

        private readonly ICameraManager _cameraManager;
        private readonly IDrivingGuide _drivingGuide;
        protected DrivingRouteObject _drivingRouteObject;
        private readonly IDrivingRouteObjectFactory _drivingRouteObjectFactory;
        private readonly IInternalUserLocationLayerPresenter _locationLayerPresenter;
        private readonly IInternalLocationService _locationService;
        private readonly ILogger _logger;
        private readonly IMapCameraPositionPresenter _mapCameraPositionPresenter;

        protected readonly IObservable<(PolylinePosition position, DrivingRouteObject route)> _positionWithRouteStream;
        private readonly GenericMapObjectCollectionSync<DrivingRouteObject, DrivingRouteGuideMapObject> _routeMapObjects;

        private readonly RoutesOverviewPresenter _routesOverviewPresenter;
        private readonly ISettingsRepository _settingsRepository;

        private readonly ViaPointsSessionManager _viaPointsSessionManager;

        #endregion

        #region Fields

        private IDisposable _subscriptions;

        #endregion

        #region Constructors

        protected BaseDrivingGuideSession(DrivingRouteObject drivingRouteObject,
            RoutesOverviewPresenterFactory routesOverviewPresenterFactory,
            IDrivingGuide drivingGuide,
            IInternalUserLocationLayerPresenter locationLayerPresenter,
            IInternalLocationService locationService,
            ICameraManager cameraManager,
            IMapCameraPositionPresenter mapCameraPositionPresenter,
            ISettingsRepository settingsRepository,
            ViaPointsSessionManagerFactory viaPointsSessionManagerFactory,
            ILogManager logManager,
            IDrivingRouteObjectFactory drivingRouteObjectFactory,
            MapObjectCollection mapObjectCollection,
            IGenericRendererSync<DrivingRouteObject, DrivingRouteGuideMapObject> guideRouteRenderer)
        {
            _drivingRouteObject = drivingRouteObject;
            _drivingGuide = drivingGuide;
            _locationLayerPresenter = locationLayerPresenter;
            _locationService = locationService;
            _cameraManager = cameraManager;
            _mapCameraPositionPresenter = mapCameraPositionPresenter;
            _settingsRepository = settingsRepository;
            _drivingRouteObjectFactory = drivingRouteObjectFactory;
            _logger = logManager.GetLogger(Scopes.Driving, LogManagerFactory.DefaultConfiguration);

            _routeMapObjects = new GenericMapObjectCollectionSync<DrivingRouteObject, DrivingRouteGuideMapObject>(mapObjectCollection, guideRouteRenderer);

            async Task<IRouteData> MeasureEmitRouteData(Route r)
            {
                using (new TimeIt("EmitRouteData", _logger))
                {
                    return await EmitRouteData(r);
                }
            }

            RouteStream = _drivingGuide.RouteChanged.Do(r => Debug.WriteLine($"RouteChanged {r?.RouteId}"))
                .Select(route => MeasureEmitRouteData(route).ToObservable())
                .Switch()
                .Where(r => r != null)
                .ObserveOnDispatcher()
                .Publish()
                .RefCount();

            _positionWithRouteStream = _drivingGuide.PositionChanged.Select(position => new { position, guideRoute = _drivingGuide.Route })
                .Where(t => t.position != null)
                .WithLatestFrom(RouteStream, (t, routeData) => new { t.position, routeData, t.guideRoute })
                .Where(t => t.routeData.DrivingRouteObject.Route.RouteId == t.guideRoute.RouteId)
                .Select(t => (t.position, t.routeData.DrivingRouteObject))
                .Publish()
                .RefCount();

            RarePostionStream = _positionWithRouteStream.Sample(TimeSpan.FromSeconds(0.3)).ObserveOnDispatcher().Publish().RefCount();

            _routesOverviewPresenter = routesOverviewPresenterFactory.Create(RouteStream.Select(it => it.DrivingRouteObject));

            _viaPointsSessionManager = viaPointsSessionManagerFactory.Create(logManager, RouteStream.Select(it => it.DrivingRouteObject));

            FinishDragged = RouteStream.Select(it => it.DrivingRouteObject.FinishDragged).Switch().Publish().RefCount();

            RoutePositionChanged = RarePostionStream.Select(it => GetRoutePosition(it.position, it.route.Route.RouteId)).Publish().RefCount();
        }

        #endregion

        #region Properties

        protected IObservable<(PolylinePosition position, DrivingRouteObject route)> RarePostionStream { get; }

        protected IObservable<IRouteData> RouteStream { get; }

        #endregion

        #region Methods

        protected abstract Task<IRouteData> EmitRouteData(Route route);

        protected virtual IDisposable Subscribe()
        {
            return new CompositeDisposable(_positionWithRouteStream.Subscribe(p => p.route.ChangeRoutePosition(p.position)),
                _routesOverviewPresenter.SelectedRouteStream.Subscribe(_drivingGuide.SetRoute),
                InitViaPointsStreams());
        }

        [CanBeNull]
        protected DrivingRouteObject EmitDrivingRouteObject([CanBeNull] Route route)
        {
            using (new TimeIt("EraseRoute", _logger))
                EraseRoute();
            if (route == null)
                return null;
            DrivingRouteObject dro;
            using (new TimeIt("_drivingRouteObjectFactory.Create(route)", _logger))
                dro = _drivingRouteObjectFactory.Create(route);
            using (new TimeIt("DrawRoute(dro)", _logger))
                DrawRoute(dro);
            return dro;
        }

        private void DrawRoute([NotNull] DrivingRouteObject drivingRouteObject)
        {
            using (new TimeIt("drivingRouteObject.IsSelected = true", _logger))
                drivingRouteObject.IsSelected = true;
            using (new TimeIt("_routeMapObjects.Add(drivingRouteObject)", _logger))
                _routeMapObjects.Add(drivingRouteObject);
        }

        private void EraseRoute()
        {
            _routeMapObjects.Clear();
        }

        private RoutePosition GetRoutePosition(PolylinePosition postition, string routeId)
        {
            Weight restRouteWeight = _drivingGuide.Route?.Metadata?.Weight;
            return new RoutePosition(restRouteWeight?.Distance.ToModel(), restRouteWeight?.TimeWithTraffic?.ToModel(), postition.ToPolylinePosition(), routeId);
        }

        private Task InitLocationSourceAndCamera()
        {
            _locationLayerPresenter.SetLocationViewSource(_drivingGuide.CreateLocationViewSource());
            _locationService.SetLocationSource(new GuideLocationSource(_drivingGuide));
            _cameraManager.SetGuidanceMode(16f, _settingsRepository.GuidanceTilt);
            return _locationLayerPresenter.SwitchToDrivingGuide();
        }

        private IDisposable InitViaPointsStreams()
        {
            async Task RouteChanged(Route route)
            {
                if (_drivingGuide.Route != null)
                    _drivingGuide.SetRoute(route);
                else
                {
                    await StopGuide();
                    _drivingRouteObject = _drivingRouteObjectFactory.Create(route);
                    await StartGuide(CancellationToken.None);
                }
            }

            return _viaPointsSessionManager.ChangedRouteStream.Select(r => RouteChanged(r).ToObservable()).Switch().Subscribe();
        }

        private Task ReturnLocationSourceAndCameraToDefault()
        {
            LocationViewSource locationManagerViewSource = LocationViewSourceFactory.CreateLocationViewSource(SystemHelper.LocationManager);
            _locationLayerPresenter.SetLocationViewSource(locationManagerViewSource);
            _locationService.SetLocationSource(new LocationManagerLocationSource(SystemHelper.LocationManager));
            _cameraManager.ExitFromGuidanceMode();
            _settingsRepository.GuidanceTilt = _mapCameraPositionPresenter.CurrentCameraPosition.Tilt;
            return _locationLayerPresenter.SwitchToDefault();
        }

        #endregion

        #region IDrivingGuideSession Members

        public IViaPointsSessionManager ViaPointsSessionManager => _viaPointsSessionManager;

        public void HideAlternativesOverview() => _routesOverviewPresenter.HideRoutesOvetview();

        public void ShowAlternativesOverview() => _routesOverviewPresenter.ShowRoutesOvetview();

        public virtual async Task StartGuide(CancellationToken token)
        {
            using (new TimeIt("StartGuide", _logger))
            {
                using (new TimeIt("_subscriptions = Subscribe()", _logger))
                    _subscriptions = Subscribe();
                using (new TimeIt("_drivingGuide.Start(_drivingRouteObject.Route)", _logger))
                    _drivingGuide.Start(_drivingRouteObject.Route);
                using (new TimeIt(" _drivingGuide.Resume()", _logger))
                    _drivingGuide.Resume();
                using (new TimeIt("await InitLocationSourceAndCamera()", _logger))
                    await InitLocationSourceAndCamera();

                token.ThrowIfCancellationRequested();
            }
        }

        public virtual Task StopGuide()
        {
            _drivingGuide.Stop();
            _subscriptions?.Dispose();
            _routesOverviewPresenter.StopGuide();
            _viaPointsSessionManager.Dispose();
            return ReturnLocationSourceAndCameraToDefault();
        }

        #endregion

        #region ISimpleDrivingSessionStreams Members

        public IObservable<Coordinate> FinishDragged { get; }

        public IObservable<Unit> Finished => _drivingGuide.Finished;
        public IObservable<bool> IsRoutesOverviewShownStream => _routesOverviewPresenter.OverviewShownStream;

        public IObservable<RoutePosition> RoutePositionChanged { get; }

        #endregion
    }
}
