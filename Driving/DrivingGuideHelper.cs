using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Company.System.Driving;
using Company.System.Geometry;
using Company.App.Services.Contracts.Guidance.Driving;
using Company.App.Services.Contracts.Route;
using Company.App.Services.Extensions;

namespace Company.App.Services.Guidance.Driving
{
    internal static class DrivingGuideHelper
    {
        #region Static Methods

        public static double GetDistanceToEvent(PolylinePosition currentPosition, PolylinePosition nextEventPosition, IReadOnlyList<RouteSectionDistanceInfo> sections)
        {
            Debug.Assert(sections.Count > currentPosition.SegmentIndex, "Current section index out of randge sections collection");
            Debug.Assert(sections.Count > nextEventPosition.SegmentIndex, "Next event section index out of randge sections collection");

            if (!(sections.Count > currentPosition.SegmentIndex && sections.Count > nextEventPosition.SegmentIndex))
                return 0;
            
            RouteSectionDistanceInfo curSection = sections[(int)currentPosition.SegmentIndex];
            RouteSectionDistanceInfo eventSection = sections[(int)nextEventPosition.SegmentIndex];

            double distance = eventSection.DistanceFromStart + eventSection.Length * nextEventPosition.SegmentPosition - curSection.DistanceFromStart
                              - curSection.Length * currentPosition.SegmentPosition;

            return distance;
        }

        public static DrivingGuideProgressInfo GetProgressInfo(Route route, IReadOnlyList<RouteSectionDistanceInfo> sectionDistanceInfos)
        {
            IReadOnlyList<RouteJamType> jamSegs = route.JamSegments.Select(s => (RouteJamType)s.JamType).ToList();
            IReadOnlyList<uint> viaPointIndexes = route.GetViaPointIndexes();

            IReadOnlyList<DrivingGuideProgressSection> sections = sectionDistanceInfos
                .Zip(jamSegs, (info, seg) => new DrivingGuideProgressSection(seg, info.Length, info.DistanceFromStart))
                .ToList();

            Tuple<DrivingGuideProgressSection[], DrivingGuideProgressSection> reducedTuple = sections.Skip(1)
                .Aggregate(Tuple.Create(new DrivingGuideProgressSection[0], sections[0]),
                    (tuple, section) =>
                    {
                        DrivingGuideProgressSection lastSec = tuple.Item2;
                        return lastSec.JamType == section.JamType
                            ? Tuple.Create(tuple.Item1, lastSec.AddLength(section.Length))
                            : Tuple.Create(tuple.Item1.Concat(new[] { lastSec }).ToArray(), section);
                    });

            IReadOnlyList<DrivingGuideProgressSection> reducedSections = reducedTuple.Item1.Concat(new[] { reducedTuple.Item2 }).ToList();

            return new DrivingGuideProgressInfo(sections, reducedSections.ToArray(), viaPointIndexes, route.RouteId);
        }

        public static DrivingGuideProgressInfo GetProgressInfo(string routeId, IEnumerable<RouteJamType> jamSegs, IEnumerable<Section> sectionsNoCoro, IReadOnlyList<RouteSectionDistanceInfo> sectionDistanceInfos)
        {
            IReadOnlyList<uint> viaPointIndexes = sectionsNoCoro.GetViaPointIndexes();

            IReadOnlyList<DrivingGuideProgressSection> sections = sectionDistanceInfos
                .Zip(jamSegs, (info, seg) => new DrivingGuideProgressSection(seg, info.Length, info.DistanceFromStart))
                .ToList();

            Tuple<DrivingGuideProgressSection[], DrivingGuideProgressSection> reducedTuple = sections.Skip(1)
                .Aggregate(Tuple.Create(new DrivingGuideProgressSection[0], sections[0]),
                    (tuple, section) =>
                    {
                        DrivingGuideProgressSection lastSec = tuple.Item2;
                        return lastSec.JamType == section.JamType
                            ? Tuple.Create(tuple.Item1, lastSec.AddLength(section.Length))
                            : Tuple.Create(tuple.Item1.Concat(new[] { lastSec }).ToArray(), section);
                    });

            IReadOnlyList<DrivingGuideProgressSection> reducedSections = reducedTuple.Item1.Concat(new[] { reducedTuple.Item2 }).ToList();

            return new DrivingGuideProgressInfo(sections, reducedSections.ToArray(), viaPointIndexes, routeId);
        }

        #endregion
    }
}
