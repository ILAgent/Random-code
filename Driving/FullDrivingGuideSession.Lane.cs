using System.Collections.Generic;
using System.Linq;
using Company.App.Services.Contracts.Guidance.Driving.Lanes;

namespace Company.App.Services.Guidance.Driving
{
    internal partial class FullDrivingGuideSession
    {
        #region Nested type: Lane

        private struct Lane : ILane
        {
            #region Constructors

            public Lane(System.Driving.Lane lane)
            {
                Directions = lane.Directions.Select(ConvertDirection).ToList();
                HighlightedDirection = lane.HighlightedDirection != null ? ConvertDirection(lane.HighlightedDirection.Value) : (LaneDirection?)null;
                Kind = ConvertKind(lane.LaneKind);
            }

            #endregion

            #region Static Methods

            private static LaneDirection ConvertDirection(System.Driving.LaneDirection direction)
            {
                switch (direction)
                {
                    case System.Driving.LaneDirection.UnknownDirection: return LaneDirection.UnknownDirection;
                    case System.Driving.LaneDirection.Left180: return LaneDirection.Left180;
                    case System.Driving.LaneDirection.Left135: return LaneDirection.Left135;
                    case System.Driving.LaneDirection.Left90: return LaneDirection.Left90;
                    case System.Driving.LaneDirection.Left45: return LaneDirection.Left45;
                    case System.Driving.LaneDirection.StraightAhead: return LaneDirection.StraightAhead;
                    case System.Driving.LaneDirection.Right45: return LaneDirection.Right45;
                    case System.Driving.LaneDirection.Right90: return LaneDirection.Right90;
                    case System.Driving.LaneDirection.Right135: return LaneDirection.Right135;
                    case System.Driving.LaneDirection.Right180: return LaneDirection.Right180;
                    case System.Driving.LaneDirection.LeftFromRight: return LaneDirection.LeftFromRight;
                    case System.Driving.LaneDirection.RightFromLeft: return LaneDirection.RightFromLeft;
                    case System.Driving.LaneDirection.LeftShift: return LaneDirection.LeftShift;
                    case System.Driving.LaneDirection.RightShift: return LaneDirection.RightShift;
                    default: return LaneDirection.UnknownDirection;
                }
            }

            private static LaneKind ConvertKind(System.Driving.LaneKind kind)
            {
                switch (kind)
                {
                    case System.Driving.LaneKind.UnknownKind: return LaneKind.UnknownKind;
                    case System.Driving.LaneKind.PlainLane: return LaneKind.PlainLane;
                    case System.Driving.LaneKind.BusLane: return LaneKind.BusLane;
                    case System.Driving.LaneKind.TramLane: return LaneKind.TramLane;
                    case System.Driving.LaneKind.TaxiLane: return LaneKind.TaxiLane;
                    case System.Driving.LaneKind.BikeLane: return LaneKind.BikeLane;
                    default: return LaneKind.UnknownKind;
                }
            }

            #endregion

            #region ILane Members

            public IEnumerable<LaneDirection> Directions { get; }

            public LaneDirection? HighlightedDirection { get; }

            public LaneKind Kind { get; }

            #endregion
        }

        #endregion
    }
}
