namespace Company.App.Services.Guidance.Driving
{
    internal class RouteSectionDistanceInfo
    {
        #region Constructors

        public RouteSectionDistanceInfo(double length, double distanceFromStart)
        {
            Length = length;
            DistanceFromStart = distanceFromStart;
        }

        #endregion

        #region Properties

        public double DistanceFromStart { get; }

        public double Length { get; }

        #endregion
    }
}
