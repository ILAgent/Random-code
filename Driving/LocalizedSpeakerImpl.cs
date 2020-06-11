using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using MetroLog;
using Microsoft.Practices.ObjectBuilder2;
using Company.System.Guidance;
using Company.App.Services.Contracts;
using Company.App.Services.Contracts.Logging;
using Company.App.Services.Contracts.UserData;
using Company.App.Utils.Logging;

namespace Company.App.Services.Guidance.Driving
{
    internal class LocalizedSpeakerImpl : LocalizedSpeaker, IDisposable
    {
        #region Static and Readonly Fields

        private readonly ILifecycleService _lifecycleService;

        private readonly ILogger _logger;

        private readonly MediaElement _mediaElement;
        private readonly ISettingsRepository _settingsRepository;
        private readonly VoiceFileHelper _voiceFileHelper;

        #endregion

        #region Fields

        private IDisposable _lifecycleSubscription;

        #endregion

        #region Constructors

        public LocalizedSpeakerImpl(ILogManager logManager, ISettingsRepository settingsRepository, ILifecycleService lifecycleService)
        {
            _settingsRepository = settingsRepository;
            _lifecycleService = lifecycleService;
            _mediaElement = new MediaElement();

            _voiceFileHelper = new VoiceFileHelper(logManager);
            _logger = logManager.GetLogger(Scopes.Driving, LogManagerFactory.DefaultConfiguration);
        }

        #endregion

        #region Methods

        public Task Init()
        {
            _lifecycleSubscription = _lifecycleService.Suspending.Subscribe(_ => _mediaElement.Stop());
            return _voiceFileHelper.InitDurations();
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _lifecycleSubscription?.Dispose();
        }

        #endregion

        #region LocalizedSpeaker Members

        double LocalizedSpeaker.Duration(LocalizedPhrase phrase)
        {
            return _voiceFileHelper.GetDuration(phrase, _settingsRepository.GuideSpeakerSex);
        }

        void LocalizedSpeaker.Reset()
        {
            _logger.Method().Start();
            try
            {
                _mediaElement.Stop();
                _logger.Method().End();
            }
            catch (Exception exception)
            {
                _logger.Method().Exception(exception);
            }
        }

        void LocalizedSpeaker.Say(LocalizedPhrase phrase)
        {
            _logger.Method().Start(phrase.Text());

            try
            {
                _mediaElement.Stop();

                MediaPlaybackItem[] items = Enumerable.Range(0, Convert.ToInt32(phrase.TokensCount()))
                    .Select(Convert.ToUInt32)
                    .Select(phrase.Token)
                    .Select(token => _voiceFileHelper.GetFileUri(token, phrase.Language(), _settingsRepository.GuideSpeakerSex))
                    .Where(uri => uri != null)
                    .Select(MediaSource.CreateFromUri)
                    .Select(src => new MediaPlaybackItem(src))
                    .ToArray();

                var playList = new MediaPlaybackList();
                items.ForEach(playList.Items.Add);

                _mediaElement.SetPlaybackSource(playList);
                _mediaElement.Play();
                _logger.Method().End();
            }
            catch (Exception exception)
            {
                _logger.Method().Exception(exception);
            }
        }

        #endregion
    }
}
