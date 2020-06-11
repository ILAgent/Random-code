using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Company.System.Driving;
using Company.System.Geometry;
using Company.System.RoadEvents;
using Company.App.Services.Contracts.Guidance.Driving;

namespace Company.App.Services.Guidance.Driving
{
    internal static class RouteEventsHelper
    {
        #region Constants

        private const double MettersToEventLowerLimit = 150.0;
        private const double MettersToEventUpperLimit = 500.0;
        private const double SecondsToEventUpperLimit = 15.0;

        #endregion

        #region Static Methods

        public static (DisplayRouteEventType? Event, double? Distances) GetEventsAndDistanceStreams(PolylinePosition polylinePosition,
            double speed,
            Queue<Event> events,
            IReadOnlyList<RouteSectionDistanceInfo> sections)
        {
            if (!events.Any())
                return (null, null);
            Event nextEvent = events.Peek();
            double distanceToEvent = DrivingGuideHelper.GetDistanceToEvent(polylinePosition, nextEvent.PolylinePosition, sections);
            if (distanceToEvent <= 0.0)
            {
                events.Dequeue();
                return (DisplayRouteEventType.None, null);
            }
            double timeToEvent = distanceToEvent / speed;
            if (timeToEvent < SecondsToEventUpperLimit && distanceToEvent <= MettersToEventUpperLimit && distanceToEvent >= MettersToEventLowerLimit)
            {
                return ( GetDisplayEventType(nextEvent), distanceToEvent );
            }
            return (null, distanceToEvent );
        }

        private static DisplayRouteEventType? GetDisplayEventType(Event @event)
        {
            if (@event == null)
                return null;
            switch (@event.Types[0])
            {
                case EventType.SpeedCamera: return DisplayRouteEventType.SpeedCamera;
                case EventType.LaneCamera: return DisplayRouteEventType.LaneCamera;
                case EventType.PolicePost: return DisplayRouteEventType.PolicePost;
                default:
                    Debug.Assert(false);
                    return null;
            }
        }

        #endregion
    }
}
