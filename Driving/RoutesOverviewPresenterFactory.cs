using System;
using MetroLog;
using Company.App.Services.Contracts.Localization;
using Company.App.Services.Contracts.Map.Presenters;
using Company.App.Services.Map.Models.Route;
using Company.App.Services.Map.Renderers.Route;
using Company.App.Services.Routing;

namespace Company.App.Services.Guidance.Driving
{
    internal class RoutesOverviewPresenterFactory
    {
        #region Static and Readonly Fields

        private readonly Lazy<AlternativeRoutesRenderer> _alternativeRoutesRenderer;
        private readonly Lazy<ICameraManager> _cameraManager;
        private readonly Lazy<IMapCameraPositionPresenter> _cameraPresenter;
        private readonly Lazy<IDrivingGuide> _dringGuide;
        private readonly Lazy<IDrivingRouteObjectFactory> _drivingRouteObjectFactory;
        private readonly Lazy<IInternalRouteService> _internalRouteService;
        private readonly Lazy<ILocalizationService> _localizationService;
        private readonly ILogManager _logManager;

        #endregion

        #region Constructors

        public RoutesOverviewPresenterFactory(Lazy<AlternativeRoutesRenderer> alternativeRoutesRenderer,
            Lazy<IInternalRouteService> internalRouteService,
            Lazy<ILocalizationService> localizationService,
            Lazy<IMapCameraPositionPresenter> cameraPresenter,
            Lazy<IDrivingGuide> dringGuide,
            Lazy<ICameraManager> cameraManager,
            Lazy<IDrivingRouteObjectFactory> drivingRouteObjectFactory,
            ILogManager logManager)
        {
            _alternativeRoutesRenderer = alternativeRoutesRenderer;
            _internalRouteService = internalRouteService;
            _localizationService = localizationService;
            _cameraPresenter = cameraPresenter;
            _dringGuide = dringGuide;
            _cameraManager = cameraManager;
            _drivingRouteObjectFactory = drivingRouteObjectFactory;
            _logManager = logManager;
        }

        #endregion

        #region Methods

        public RoutesOverviewPresenter Create(IObservable<DrivingRouteObject> routesStream)
        {
            return new RoutesOverviewPresenter(_alternativeRoutesRenderer,
                _internalRouteService,
                _localizationService,
                _cameraPresenter,
                _dringGuide,
                _cameraManager,
                _drivingRouteObjectFactory,
                _logManager,
                routesStream);
        }

        #endregion
    }
}
