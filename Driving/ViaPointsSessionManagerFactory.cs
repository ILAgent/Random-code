using System;
using MetroLog;
using Company.App.Services.Contracts.Routing;
using Company.App.Services.Contracts.Search;
using Company.App.Services.Location;
using Company.App.Services.Map.Models.Route;
using Company.App.Services.Routing;

namespace Company.App.Services.Guidance.Driving
{
    internal class ViaPointsSessionManagerFactory
    {
        #region Static and Readonly Fields

        private readonly IInternalRouteService _internalRouteService;
        private readonly IInternalLocationService _locationService;
        private readonly IRouteTimeService _routeTimeService;
        private readonly ISearchService _searchService;

        #endregion

        #region Constructors

        public ViaPointsSessionManagerFactory(IInternalRouteService internalRouteService,
            IInternalLocationService locationService,
            ISearchService searchService,
            IRouteTimeService routeTimeService)
        {
            _internalRouteService = internalRouteService;
            _locationService = locationService;
            _searchService = searchService;
            _routeTimeService = routeTimeService;
        }

        #endregion

        #region Methods

        public ViaPointsSessionManager Create(ILogManager logManager, IObservable<DrivingRouteObject> routesStream)
        {
            return new ViaPointsSessionManager(logManager, _internalRouteService, _locationService, _searchService, _routeTimeService, routesStream);
        }

        #endregion
    }
}
