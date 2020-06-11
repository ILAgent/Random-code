using Company.App.Services.Contracts.Location;
using Company.App.Services.Contracts.Models.MapObjects;

namespace Company.App.Services.Guidance.Driving
{
    internal interface IViaPointData
    {
        #region Properties

        string Uri { get; }

        Coordinate UserObjectCoordinate { get; }

        #endregion

        #region Methods

        bool IsSameAs(IPoiModel poiModel);

        #endregion
    }

    internal class ViaPointToponymData : IViaPointData
    {
        #region Static and Readonly Fields

        private readonly Coordinate _geometryPosition;

        #endregion

        #region Constructors

        public ViaPointToponymData(Coordinate userObjectCoordinate, Coordinate geometryPosition, string uri)
        {
            UserObjectCoordinate = userObjectCoordinate;
            _geometryPosition = geometryPosition;
            Uri = uri;
        }

        #endregion

        #region IViaPointData Members

        public string Uri { get; }

        public Coordinate UserObjectCoordinate { get; }

        public bool IsSameAs(IPoiModel poiModel)
        {
            var visitor = new PoiVisitor<bool>(t => t.GeometryPosition == _geometryPosition, b => false, s => false);
            poiModel.AcceptVisitor(visitor);
            return visitor.Result;
        }

        #endregion
    }

    internal class ViaPointBusinessOrStopData : IViaPointData
    {
        #region Constructors

        public ViaPointBusinessOrStopData(Coordinate userObjectCoordinate, string uri)
        {
            UserObjectCoordinate = userObjectCoordinate;
            Uri = uri;
        }

        #endregion

        #region IViaPointData Members

        public string Uri { get; }

        public Coordinate UserObjectCoordinate { get; }

        public bool IsSameAs(IPoiModel poiModel)
        {
            var visitor = new PoiVisitor<bool>(t => false, b => b.Uri == Uri, s => s.Uri == Uri);
            poiModel.AcceptVisitor(visitor);
            return visitor.Result;
        }

        #endregion
    }
}
