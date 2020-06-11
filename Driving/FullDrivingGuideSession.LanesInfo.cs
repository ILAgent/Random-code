using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using JetBrains.Annotations;
using Company.System.Driving;
using Company.System.Geometry;
using Company.App.Services.Contracts.Guidance.Driving.Lanes;

namespace Company.App.Services.Guidance.Driving
{
    internal partial class FullDrivingGuideSession
    {
        #region Nested type: LanesInfo

        private class LanesInfo : ILanesInfo
        {
            #region Static and Readonly Fields

            private readonly BehaviorSubject<double> _distanceBehaviorSubject;
            private readonly PolylinePosition _lanesPosition;
            private readonly IReadOnlyList<RouteSectionDistanceInfo> _sections;

            #endregion

            #region Constructors

            public LanesInfo([NotNull] LaneSign laneSign, [NotNull] PolylinePosition currentPosition, [NotNull] IReadOnlyList<RouteSectionDistanceInfo> sections)
            {
                if (laneSign == null)
                    throw new ArgumentNullException(nameof(laneSign));
                if (currentPosition == null)
                    throw new ArgumentNullException(nameof(currentPosition));
                if (sections == null)
                    throw new ArgumentNullException(nameof(sections));

                Lanes = laneSign.Lanes.Select(l => (ILane)new Lane(l)).ToList();
                _lanesPosition = laneSign.Position;
                _sections = sections;
                double distance = DrivingGuideHelper.GetDistanceToEvent(currentPosition, laneSign.Position, sections);
                _distanceBehaviorSubject = new BehaviorSubject<double>(distance);
            }

            #endregion

            #region Methods

            public void ChangeDistance(PolylinePosition currentPosition)
            {
                double distance = DrivingGuideHelper.GetDistanceToEvent(currentPosition, _lanesPosition, _sections);
                _distanceBehaviorSubject.OnNext(distance);
            }

            #endregion

            #region ILanesInfo Members

            public IObservable<double> DistanceToLanesStream => _distanceBehaviorSubject;

            public IReadOnlyList<ILane> Lanes { get; }

            #endregion
        }

        #endregion
    }
}
