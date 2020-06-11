using Company.App.Services.Map.Models.Route;

namespace Company.App.Services.Guidance.Driving
{
    internal interface IDrivingRouteObjectHolder
    {
        #region Properties

        DrivingRouteObject DrivingRouteObject { get; set; }

        #endregion
    }
}
