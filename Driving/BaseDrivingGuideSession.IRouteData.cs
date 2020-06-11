using Company.App.Services.Map.Models.Route;

namespace Company.App.Services.Guidance.Driving
{
    internal abstract partial class BaseDrivingGuideSession
    {
        #region Nested type: IRouteData

        protected interface IRouteData
        {
            #region Properties

            DrivingRouteObject DrivingRouteObject { get; }

            #endregion
        }

        #endregion
    }
}
