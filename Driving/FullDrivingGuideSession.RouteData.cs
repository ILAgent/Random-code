using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MetroLog;
using Company.System.Driving;
using Company.System.Geometry;
using Company.System.RoadEvents;
using Company.App.Services.Contracts.Guidance.Driving;
using Company.App.Services.Contracts.Route;
using Company.App.Services.Extensions;
using Company.App.Services.Map.Models.Route;
using Company.App.Utils;

namespace Company.App.Services.Guidance.Driving
{
    internal partial class FullDrivingGuideSession
    {
        #region Nested type: FullGuideRouteData

        private class FullGuideRouteData : IRouteData
        {
            #region Constructors

            private FullGuideRouteData(IEnumerable<Event> eventsNoCoro,
                IEnumerable<RouteJamType> jamSegs,
                IEnumerable<Section> sectionsNoCoro,
                DrivingRouteObject drivingRouteObject,
                ILogger logger)
            {
                DrivingRouteObject = drivingRouteObject;

                Route route = drivingRouteObject.Route;

                using (new TimeIt("GetRouteSections", logger))
                    Sections = GetRouteSections(route, logger);
                using (new TimeIt("GetRouteDisplayEvents", logger))
                    Events = GetRouteDisplayEvents(eventsNoCoro);
                using (new TimeIt("GetProgressInfo", logger))
                    ProgressInfo = DrivingGuideHelper.GetProgressInfo(route.RouteId, jamSegs, sectionsNoCoro, Sections);
            }

            #endregion

            #region Properties

            public Queue<Event> Events { get; }
            public DrivingGuideProgressInfo ProgressInfo { get; }

            public IReadOnlyList<RouteSectionDistanceInfo> Sections { get; }

            #endregion

            #region Static Methods

            public static Task<FullGuideRouteData> Create(DrivingRouteObject drivingRouteObject, ILogger logger)
            {
                Route route = drivingRouteObject.Route;

                IEnumerable<Event> eventsNoCoro = route.Events.AsEnumerable();

                IEnumerable<RouteJamType> jamSegs = route.JamSegments.Select(s => (RouteJamType)s.JamType);

                IEnumerable<Section> sectionsNoCoro = route.Sections.AsEnumerable();

                return Task.Run(() => new FullGuideRouteData(eventsNoCoro, jamSegs, sectionsNoCoro, drivingRouteObject, logger));
            }

            private static Queue<Event> GetRouteDisplayEvents(IEnumerable<Event> eventsNoCoro)
            {
                var displayEventTypes = new HashSet<EventType> { EventType.LaneCamera, EventType.SpeedCamera, EventType.PolicePost };
                IEnumerable<Event> displayEvents = eventsNoCoro.Where(e => displayEventTypes.Contains(e.Types[0]));
                return new Queue<Event>(displayEvents);
            }

            private static IReadOnlyList<RouteSectionDistanceInfo> GetRouteSections(Route route, ILogger logger)
            {
                IReadOnlyList<Point> startPoints;
                using (new TimeIt("route.Geometry.Points.ToList()",logger))
                    startPoints = route.Geometry.Points.ToList();
                IEnumerable<Point> endPoints = startPoints.Skip(1);
                IEnumerable<double> lengths = startPoints.Zip(endPoints, (p1, p2) => p1.DistanceTo(p2));

                var sectionDistanceInfos = new List<RouteSectionDistanceInfo>();
                var distanceFromStart = 0.0;
                foreach (double length in lengths)
                {
                    sectionDistanceInfos.Add(new RouteSectionDistanceInfo(length, distanceFromStart));
                    distanceFromStart += length;
                }
                return sectionDistanceInfos;
            }

            #endregion

            #region IRouteData Members

            public DrivingRouteObject DrivingRouteObject { get; }

            #endregion
        }

        #endregion
    }
}
