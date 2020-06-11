using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using MetroLog;
using Company.System.Driving;
using Company.System.Geometry;
using Company.System.Guidance;
using Company.System.Map;
using Company.App.Services.Contracts;
using Company.App.Services.Contracts.Driving;
using Company.App.Services.Contracts.Guidance.Driving;
using Company.App.Services.Contracts.Guidance.Driving.Lanes;
using Company.App.Services.Contracts.Logging;
using Company.App.Services.Contracts.Map.Presenters;
using Company.App.Services.Contracts.UserData;
using Company.App.Services.Core.Guidance;
using Company.App.Services.Extensions;
using Company.App.Services.Location;
using Company.App.Services.Map.Models.Route;
using Company.App.Services.Map.Presenters;
using Company.App.Services.Map.Renderers;
using Company.App.Utils;
using Company.App.Utils.Extensions;
using Action = Company.System.Driving.Action;

namespace Company.App.Services.Guidance.Driving
{
    internal partial class FullDrivingGuideSession : BaseDrivingGuideSession, IFullDrivingGuideSession, IFullDrivingSessionStreams
    {
        #region Static and Readonly Fields

        private readonly IDrivingGuide _drivingGuide;
        private readonly LanesShowingManager _lanesShowingManager = new LanesShowingManager();
        private readonly ILanguagesRepository _languagesRepository;
        private readonly LocalizedSpeakerImpl _localizedSpeaker;

        private readonly IObservable<(PolylinePosition position, FullGuideRouteData routeData)> _positionWithRouteDataStream;

        private readonly ISettingsRepository _settingsRepository;

        #endregion

        #region Constructors

        private readonly ILogger _logger;

        public FullDrivingGuideSession(DrivingRouteObject drivingRouteObject,
            IDrivingGuide drivingGuide,
            IDrivingRouteObjectFactory drivingRouteObjectFactory,
            IGenericRendererSync<DrivingRouteObject, DrivingRouteGuideMapObject> guideRouteRenderer,
            MapObjectCollection mapObjectCollection,
            IInternalLocationService locationService,
            IInternalUserLocationLayerPresenter locationLayerPresenter,
            ILogManager logManager,
            ISettingsRepository settingsRepository,
            ILifecycleService lifecycleService,
            ILanguagesRepository languagesRepository,
            ICameraManager cameraManager,
            IMapCameraPositionPresenter cameraPositionPresenter,
            ViaPointsSessionManagerFactory viaPointsSessionManagerFactory,
            RoutesOverviewPresenterFactory routesOverviewPresenterFactory) : base(drivingRouteObject,
            routesOverviewPresenterFactory,
            drivingGuide,
            locationLayerPresenter,
            locationService,
            cameraManager,
            cameraPositionPresenter,
            settingsRepository,
            viaPointsSessionManagerFactory,
            logManager,
            drivingRouteObjectFactory,
            mapObjectCollection,
            guideRouteRenderer)
        {
            _drivingGuide = drivingGuide;
            _settingsRepository = settingsRepository;
            _languagesRepository = languagesRepository;
            _localizedSpeaker = new LocalizedSpeakerImpl(logManager, settingsRepository, lifecycleService);
            _logger = logManager.GetLogger(Scopes.Driving, LogManagerFactory.DefaultConfiguration);

            IObservable<FullGuideRouteData> routeDataStream = RouteStream.Select(it => (FullGuideRouteData)it).Publish().RefCount();

            _positionWithRouteDataStream = _drivingGuide.PositionChanged.Where(p => p != null)
                .WithLatestFrom(routeDataStream, (pos, route) => new { pos, route })
                .Where(p => p.route.DrivingRouteObject.Route.RouteId == _drivingGuide.Route?.RouteId)
                .Select(p => (position: p.pos, routeData: p.route))
                .Publish()
                .RefCount();

            SpeedStream = _drivingGuide.CreateSpeedChangesStream();

            AnnotationsStream = _drivingGuide.AnnotationsChanged.Where(a => a?.Annotations?.Any() ?? false).Select(GetDrivingAnnotation).Publish().RefCount();

            var positionRouteSpeedStream =
                _positionWithRouteDataStream.WithLatestFrom(SpeedStream, (p, speed) => new { p.position, p.routeData.Sections, p.routeData.Events, speed });
            IObservable<(DisplayRouteEventType? Event, double? Distance)> getDIstanceAndEventsStream = positionRouteSpeedStream
                .Select(p => RouteEventsHelper.GetEventsAndDistanceStreams(p.position, p.speed, p.Events, p.Sections))
                .Publish()
                .RefCount();
            DistanceToEventStream = getDIstanceAndEventsStream.Where(it => it.Distance.HasValue)
                .Select(it => Math.Round(it.Distance.Value / 10) * 10)
                .DistinctUntilChanged()
                .Publish()
                .RefCount();
            RouteEventsStream = getDIstanceAndEventsStream.Where(it => it.Event.HasValue).Select(it => it.Event.Value).DistinctUntilChanged().Publish().RefCount();

            RouteConditionsStream = routeDataStream.Select(r => r.DrivingRouteObject.ConditionsUpdated.Select(_ => new { r.DrivingRouteObject.Route, r.Sections }))
                .Switch()
                .Select(p => DrivingGuideHelper.GetProgressInfo(p.Route, p.Sections))
                .Publish()
                .RefCount();

            RouteProgressInfoStream = routeDataStream.Select(r => r.ProgressInfo).Publish().RefCount();

            SpeedLimitStream = _drivingGuide.SpeedLimitUpdated.Select(v => v?.ToModel()).Publish().RefCount();
            
        }

        #endregion

        #region Static Methods

        private static DrivingAnnotation GetDrivingAnnotation(DisplayedAnnotations displayedAnnotations)
        {
            IReadOnlyList<AnnotationWithDistance> annotations = displayedAnnotations?.Annotations;
            Debug.Assert(annotations != null);
            AnnotationWithDistance annotation = annotations[0];

            Action? secondDra = annotations.Skip(1).FirstOrDefault()?.Annotation?.Action;
            AnnotationAction? secondAction = secondDra.HasValue ? (AnnotationAction)secondDra.Value : (AnnotationAction?)null;

            var dra = new DrivingAnnotation(annotation.Distance.ToModel(),
                (AnnotationAction)annotation.Annotation.Action.GetValueOrDefault(),
                displayedAnnotations.NextRoadName,
                secondAction);
            return dra;
        }

        private static Option<LanesInfo> GetLanesInfo(LaneSign laneSign, IReadOnlyList<RouteSectionDistanceInfo> sections, PolylinePosition routePosition)
        {
            return laneSign == null ? Option<LanesInfo>.None : Option<LanesInfo>.Some(new LanesInfo(laneSign, routePosition, sections));
        }

        #endregion

        #region Methods

        protected override async Task<IRouteData> EmitRouteData(Route route)
        {
            DrivingRouteObject dro = EmitDrivingRouteObject(route);
            using (new TimeIt("new FullGuideRouteData(dro)", _logger))
            {
                if (dro == null)
                    return null;
                return await FullGuideRouteData.Create(dro, _logger);
            }
        }

        protected override IDisposable Subscribe()
        {
            return new CompositeDisposable(base.Subscribe(), InitLanesInfoStreams());
        }

        private AnnotationLanguage GetLanguage(ILanguage lang)
        {
            if (lang == _languagesRepository.Ru)
                return AnnotationLanguage.Russian;
            if (lang == _languagesRepository.Uk)
                return AnnotationLanguage.Ukrainian;
            if (lang == _languagesRepository.Tr)
                return AnnotationLanguage.Turkish;
            return AnnotationLanguage.English;
        }

        private IDisposable InitLanesInfoStreams()
        {
            IObservable<double> distanceToManeuverStream = AnnotationsStream.Select(a => a.Distance.Value);

            IObservable<(Option<LanesInfo> LanesInfo, string RouteId)> lanesInfoStream = _drivingGuide.LaneSignUpdated
                .Select(laneSign => new { laneSign, _drivingGuide.Route?.RouteId })
                .CombineLatest(_positionWithRouteDataStream,
                    (t, p) => new { t.laneSign, t.RouteId, p.routeData.Sections, p.position, curRouteId = p.routeData.DrivingRouteObject.Route.RouteId })
                .Where(t => t.RouteId == t.curRouteId)
                .DistinctUntilChanged(t => t.laneSign?.Position)
                .Select(p => (GetLanesInfo(p.laneSign, p.Sections, p.position), p.RouteId))
                .Publish()
                .RefCount();

            return new CompositeDisposable(SpeedStream.Subscribe(_lanesShowingManager.OnSpeedChanged),
                distanceToManeuverStream.Subscribe(_lanesShowingManager.OnNextManeuverChanged),
                lanesInfoStream.Select(it => it.LanesInfo).Subscribe(l => _lanesShowingManager.OnLanesChanged(from x in l select (ILanesInfo)x)),
                _positionWithRouteStream.WithLatestFrom(lanesInfoStream, (pos, lanesInfo) => new { pos, lanesInfo.LanesInfo, lanesInfo.RouteId })
                    .Where(it => it.RouteId == it.pos.route.Route.RouteId)
                    .Subscribe(p => p.LanesInfo.IfSome(li => li.ChangeDistance(p.pos.position))));
        }

        #endregion

        #region IFullDrivingGuideSession Members

        public IFullDrivingSessionStreams Streams => this;

        public override async Task StartGuide(CancellationToken token)
        {
            await base.StartGuide(token);
            await _localizedSpeaker.Init();
            token.ThrowIfCancellationRequested();
            _drivingGuide.SetLocalizedSpeaker(_localizedSpeaker, GetLanguage(_settingsRepository.GuideSpeakerLanguage));
        }

        public override async Task StopGuide()
        {
            _localizedSpeaker.Dispose();
            await base.StopGuide();
        }

        #endregion

        #region IFullDrivingSessionStreams Members

        public IObservable<DrivingAnnotation> AnnotationsStream { get; }

        public IObservable<double> DistanceToEventStream { get; }

        public IObservable<Option<IReadOnlyList<ILane>>> LanesStream => _lanesShowingManager.LanesStream;

        public IObservable<string> RoadNameStream => _drivingGuide.RoadNameUpdated;

        public IObservable<DrivingGuideProgressInfo> RouteConditionsStream { get; }

        public IObservable<DisplayRouteEventType> RouteEventsStream { get; }

        public IObservable<DrivingGuideProgressInfo> RouteProgressInfoStream { get; }

        public IObservable<bool> SpeedLimitExceeded => _drivingGuide.SpeddLimitExceeded;

        public IObservable<LocalizedValue> SpeedLimitStream { get; }
        public IObservable<double> SpeedStream { get; }

        #endregion
    }
}
