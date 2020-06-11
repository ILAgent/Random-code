using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using MetroLog;
using Company.System.Driving;
using Company.System.Geometry;
using Company.App.Services.Contracts.Analytics;
using Company.App.Services.Contracts.Localization;
using Company.App.Services.Contracts.Logging;
using Company.App.Services.Contracts.Map.Models;
using Company.App.Services.Contracts.Map.Presenters;
using Company.App.Services.Map.Models.Route;
using Company.App.Services.Map.Models.Route.Composition;
using Company.App.Services.Map.Renderers.Route;
using Company.App.Services.Routing;
using Company.App.Utils.Extensions;
using Company.App.Utils.Logging;

namespace Company.App.Services.Guidance.Driving
{
    internal class RoutesOverviewPresenter
    {
        #region Static and Readonly Fields

        private readonly Lazy<AlternativeRoutesRenderer> _alternativeRoutesRenderer;
        private readonly Lazy<ICameraManager> _cameraManager;
        private readonly Lazy<IMapCameraPositionPresenter> _cameraPresenter;
        private readonly Lazy<IDrivingGuide> _dringGuide;
        private readonly Lazy<IDrivingRouteObjectFactory> _drivingRouteObjectFactory;
        private readonly Subject<Unit> _hideOverviewRequestSubject = new Subject<Unit>();
        private readonly Lazy<IInternalRouteService> _internalRouteService;
        private readonly Lazy<ILocalizationService> _localizationService;
        private readonly ILogger _logger;
        private readonly Subject<Unit> _showOverviewRequestSubject = new Subject<Unit>();

        #endregion

        #region Fields

        private AlternativeRoutesComposition _currentComposition;

        private IDisposable _renderDisposable;
        private IDisposable _subscriptions;

        private CancellationTokenSource _updateRoutesCompositionCts;

        #endregion

        #region Constructors

        public RoutesOverviewPresenter(Lazy<AlternativeRoutesRenderer> alternativeRoutesRenderer,
            Lazy<IInternalRouteService> internalRouteService,
            Lazy<ILocalizationService>localizationService,
            Lazy<IMapCameraPositionPresenter> cameraPresenter,
            Lazy<IDrivingGuide> dringGuide,
            Lazy<ICameraManager> cameraManager,
            Lazy<IDrivingRouteObjectFactory> drivingRouteObjectFactory,
            ILogManager logManager,
            IObservable<DrivingRouteObject> routesStream)
        {
            _logger = logManager.GetLogger(Scopes.RoutesOverview, LogManagerFactory.DefaultConfiguration);
            _alternativeRoutesRenderer = alternativeRoutesRenderer;
            _internalRouteService = internalRouteService;
            _localizationService = localizationService;
            _cameraPresenter = cameraPresenter;
            _dringGuide = dringGuide;
            _cameraManager = cameraManager;
            _drivingRouteObjectFactory = drivingRouteObjectFactory;

            IObservable<(DrivingRouteObject CurRoute, DrivingRouteObject PrevRoute)> routesChangeStream =
                routesStream.Skip(1).Zip(routesStream, (cur, prev) => (cur, prev)).Publish().RefCount();

            IObservable<DrivingRouteObject> viaPointsChangedStream = routesChangeStream.Where(it => it.CurRoute.ViaPoints.Count != it.PrevRoute.ViaPoints.Count)
                .Select(it => it.CurRoute)
                .Do(it => _logger.Info($"Via point added or removed, show routes overview, route id={it.Route.RouteId}"));
            IObservable<DrivingRouteObject> viaPointsNotChangedStream = routesChangeStream.Where(it => it.CurRoute.ViaPoints.Count == it.PrevRoute.ViaPoints.Count)
                .Select(it => it.CurRoute)
                .Do(it => _logger.Info($"Via points are not changed, update routes overview, route id={it.Route.RouteId}"));

            IObservable<DrivingRouteObject> showOverviewIfHidden = _showOverviewRequestSubject.WithLatestFrom(routesStream, (_, route) => route)
                .Do(it => _logger.Info($"Overview button tapped, show routes overview, route id={it.Route.RouteId}"))
                .Merge(viaPointsChangedStream)
                .Publish()
                .RefCount();

            OverviewShownStream = showOverviewIfHidden.Select(_ => true).Merge(_hideOverviewRequestSubject.Select(_ => false)).DistinctUntilChanged().Publish().RefCount();

            IObservable<DrivingRouteObject> updateOverviewIfShown = viaPointsNotChangedStream
                .Where(route => _currentComposition != null && _currentComposition.Routes.All(r => r.Route.RouteId != route.Route.RouteId))
                .Do(it => _logger.Info($"Route updated when overview shown, update routes overview, route id={it.Route.RouteId}"));

            IObservable<Unit> hideOverfiewIfShown = OverviewShownStream.Where(show => !show).ToUnitObservable().Do(_ => _logger.Info("Hide overview request"));

            IObservable<AlternativeRoutesComposition> compostitionsStream = updateOverviewIfShown.Merge(showOverviewIfHidden)
                .Select(r => Observable.Interval(TimeSpan.FromMinutes(1))
                    .ObserveOnDispatcher()
                    .Select(_ => (Route:r, MoveCamera:false))
                    .Do(it => _logger.Info($"Update overview by timer, route id={it.Route.Route.RouteId}"))
                    .StartWith((Route: r, MoveCamera: true)))
                .Merge(hideOverfiewIfShown.Select(_ => Observable.Empty<(DrivingRouteObject Route, bool MoveCamera)>()))
                .Switch()
                .Select(t => UpdateRoutesComposition(t.Route, t.MoveCamera)
                    .ToObservable()
                    .Catch<AlternativeRoutesComposition, OperationCanceledException>(_ => Observable.Empty<AlternativeRoutesComposition>())
                    .Catch<AlternativeRoutesComposition, Exception>(ex =>
                    {
                        ExceptionHandler.FailInDebugTrackInRelease(ex);
                        return Observable.Empty<AlternativeRoutesComposition>();
                    }))
                .Switch()
                .Do(compostion => _currentComposition = compostion);

            SelectedRouteStream = compostitionsStream.Select(c => c.AlternativeRouteSelected).Switch().Publish().RefCount();

            _subscriptions = OverviewShownStream.Where(show => !show).ToUnitObservable().Subscribe(_ => HideAlternativeRoutes());
        }

        #endregion

        #region Properties

        public IObservable<bool> OverviewShownStream { get; }

        public IObservable<Route> SelectedRouteStream { get; }

        #endregion

        #region Methods

        public void HideRoutesOvetview() => _hideOverviewRequestSubject.OnNext(Unit.Default);

        public void ShowRoutesOvetview() => _showOverviewRequestSubject.OnNext(Unit.Default);

        public void StopGuide()
        {
            _subscriptions?.Dispose();
            _renderDisposable?.Dispose();
            _currentComposition = null;
        }

        private void HideAlternativeRoutes()
        {
            _cameraManager.Value.TurnUserLocationFollowingOn();
            _cameraManager.Value.SetGuidanceMode(16f, 90);

            _renderDisposable?.Dispose();
            _currentComposition = null;
        }

        private async Task MoveCameraToOverview(AlternativeRoutesComposition composition, CancellationToken cancellationToken)
        {
            _cameraManager.Value.ExitFromGuidanceMode();
            _cameraManager.Value.TurnUserLocationFollowingOff();
            IMapCameraPositionPresenter cp = _cameraPresenter.Value;
            CameraPositionModel curCamera = cp.CurrentCameraPosition;
            const float azimutTolerance = 1f;
            if (Math.Abs(curCamera.Azimuth) > azimutTolerance)
            {
                var rotatatedCamera = new CameraPositionModel(curCamera.Target, curCamera.Zoom);
                await cp.MoveCamera(rotatatedCamera);
                cancellationToken.ThrowIfCancellationRequested();
            }
            await cp.MoveCamera(composition.BoundingBox);
        }

        private async Task<AlternativeRoutesComposition> UpdateRoutesComposition(DrivingRouteObject drivingRouteObject, bool moveCamera)
        {
            _logger.Method().Start($"Route id = {drivingRouteObject.Route.RouteId}, move camera = {moveCamera}");

            _updateRoutesCompositionCts?.Cancel();
            _updateRoutesCompositionCts = new CancellationTokenSource();

            Route route = drivingRouteObject.Route;
            IReadOnlyList<DrivingRouteObject> routes =
                await _internalRouteService.Value.GetAlternativeRouteObjects(route,
                    _dringGuide.Value.RoutePosition ?? new PolylinePosition(0, 0),
                    _updateRoutesCompositionCts.Token);
            var composition = new AlternativeRoutesComposition(_drivingRouteObjectFactory.Value.Create(drivingRouteObject.Route), routes, _localizationService.Value);

            _renderDisposable?.Dispose();
            _renderDisposable = await _alternativeRoutesRenderer.Value.Render(composition, _updateRoutesCompositionCts.Token);

            _subscriptions?.Dispose();
            _subscriptions = OverviewShownStream.Where(show => !show).ToUnitObservable().Subscribe(_ => HideAlternativeRoutes());

            if (moveCamera)
                await MoveCameraToOverview(composition, _updateRoutesCompositionCts.Token);
            return composition;
        }

        #endregion
    }
}
