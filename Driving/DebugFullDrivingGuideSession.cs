using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using MetroLog;
using Company.System;
using Company.System.Location;
using Company.System.Map;
using Company.App.Services.Contracts;
using Company.App.Services.Contracts.Concurrency;
using Company.App.Services.Contracts.DebugPanel;
using Company.App.Services.Contracts.Guidance.Driving;
using Company.App.Services.Contracts.Map.Presenters;
using Company.App.Services.Contracts.UserData;
using Company.App.Services.DebugPanel;
using Company.App.Services.Location;
using Company.App.Services.Map.Models.Route;
using Company.App.Services.Map.Presenters;
using Company.App.Services.Map.Renderers;
using Company.App.Utils.Extensions;

namespace Company.App.Services.Guidance.Driving
{
    internal class DebugFullDrivingGuideSession : FullDrivingGuideSession, IFullDrivingGuideSession
    {
        #region Static and Readonly Fields

        private readonly IConcurrencyService _concurrencyService;

        private readonly IDebugPanelSettingsRepository _debugRepository;
        private readonly IDrivingGuide _drivingGuide;


        #endregion

        #region Fields

        private IDisposable _debugSubscription;
        private LocationSimulator _simulator;

        #endregion

        #region Constructors

        public DebugFullDrivingGuideSession(DrivingRouteObject drivingRouteObject,
            IDrivingGuide drivingGuide,
            IDrivingRouteObjectFactory drivingRouteObjectFactory,
            IGenericRendererSync<DrivingRouteObject, DrivingRouteGuideMapObject> guideRouteRenderer,
            MapObjectCollection mapObjectCollection,
            IInternalLocationService locationService,
            IInternalUserLocationLayerPresenter locationLayerPresenter,
            ILogManager logManager,
            ISettingsRepository settingsRepository,
            ILifecycleService lifecycleService,
            ILanguagesRepository languagesRepository,
            ICameraManager cameraManager,
            IMapCameraPositionPresenter cameraPositionPresenter,
            ViaPointsSessionManagerFactory viaPointsSessionManagerFactory,
            RoutesOverviewPresenterFactory routesOverviewPresenterFactory,
            IConcurrencyService concurrencyService,
            IDebugPanelSettingsRepository debugRepository) : base(drivingRouteObject,
            drivingGuide,
            drivingRouteObjectFactory,
            guideRouteRenderer,
            mapObjectCollection,
            locationService,
            locationLayerPresenter,
            logManager,
            settingsRepository,
            lifecycleService,
            languagesRepository,
            cameraManager,
            cameraPositionPresenter,
            viaPointsSessionManagerFactory,
            routesOverviewPresenterFactory)
        {
            _concurrencyService = concurrencyService;
            _debugRepository = debugRepository;
            _drivingGuide = drivingGuide;
        }

        #endregion

        #region Methods

        private void OnSimulationSetingsChanged(bool enabled, double speed)
        {
            if (enabled)
            {
                _simulator.Speed = speed.KilometrsPerHourToMeterPerSecond();
                _drivingGuide.SetLocationManager(_simulator);
            }
            else
            {
                _drivingGuide.SetLocationManager(SystemHelper.LocationManager);
            }
        }

        #endregion

        #region IFullDrivingGuideSession Members

        public override async Task StartGuide(CancellationToken token)
        {
            await base.StartGuide(token);

            _simulator = SystemFactory.Instance.CreateLocationSimulator(DebugRoutePolyline.Polyline ?? _drivingRouteObject.Route.Geometry);

            var speedAndEnabledStream = _debugRepository.LocationSimulationEnabled
                .CombineLatest(_debugRepository.LocationSimulationSpeedInKmph.Throttle(TimeSpan.FromSeconds(0.3), _concurrencyService.TaskPool),
                    (enabled, speed) => new { enabled, speed })
                .ObserveOn(_concurrencyService.Dispatcher);

            _debugSubscription?.Dispose();
            _debugSubscription = new CompositeDisposable(speedAndEnabledStream.Subscribe(p => OnSimulationSetingsChanged(p.enabled, p.speed)));
        }

        async Task IDrivingGuideSession.StopGuide()
        {
            _debugSubscription?.Dispose();
            _debugSubscription = null;
            await StopGuide();
        }

        #endregion
    }
}
