using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Storage.Streams;
using JetBrains.Annotations;
using MetroLog;
using Company.System.Driving;
using Company.System.Guidance;
using Company.App.Services.Contracts.Enums;
using Company.App.Services.Contracts.Logging;

namespace Company.App.Services.Guidance.Driving
{
    internal class VoiceFileHelper
    {
        #region Static and Readonly Fields

        private static readonly Dictionary<PhraseToken, string> _fileNames = new Dictionary<PhraseToken, string>()
        {
            { PhraseToken.One, "1" },
            { PhraseToken.Two, "2" },
            { PhraseToken.Three, "3" },
            { PhraseToken.Four, "4" },
            { PhraseToken.Five, "5" },
            { PhraseToken.Six, "6" },
            { PhraseToken.Seven, "7" },
            { PhraseToken.Eight, "8" },
            { PhraseToken.Nine, "9" },
            { PhraseToken.Ten, "10" },
            { PhraseToken.Eleven, "11" },
            { PhraseToken.Twelve, "12" },
            { PhraseToken.Thirteen, "13" },
            { PhraseToken.Fourteen, "14" },
            { PhraseToken.Fifteen, "15" },
            { PhraseToken.Sixteen, "16" },
            { PhraseToken.Seventeen, "17" },
            { PhraseToken.Eighteen, "18" },
            { PhraseToken.Nineteen, "19" },
            { PhraseToken.Twenty, "20" },
            { PhraseToken.Thirty, "30" },
            { PhraseToken.Forty, "40" },
            { PhraseToken.Fifty, "50" },
            { PhraseToken.Sixty, "60" },
            { PhraseToken.Seventy, "70" },
            { PhraseToken.Eighty, "80" },
            { PhraseToken.Ninety, "90" },
            { PhraseToken.OneHundred, "100" },
            { PhraseToken.TwoHundred, "200" },
            { PhraseToken.ThreeHundred, "300" },
            { PhraseToken.FourHundred, "400" },
            { PhraseToken.FiveHundred, "500" },
            { PhraseToken.SixHundred, "600" },
            { PhraseToken.SevenHundred, "700" },
            { PhraseToken.EightHundred, "800" },
            { PhraseToken.NineHundred, "900" },
            { PhraseToken.First, "1st" },
            { PhraseToken.Second, "2nd" },
            { PhraseToken.Third, "3rd" },
            { PhraseToken.Fourth, "4th" },
            { PhraseToken.Fifth, "5th" },
            { PhraseToken.Sixth, "6th" },
            { PhraseToken.Seventh, "7th" },
            { PhraseToken.Eighth, "8th" },
            { PhraseToken.Ninth, "9th" },
            { PhraseToken.Tenth, "10th" },
            { PhraseToken.Eleventh, "11th" },
            { PhraseToken.Twelfth, "12th" },
            { PhraseToken.Kilometer, "Kilometer" },
            { PhraseToken.Kilometers, "Kilometers" },
            { PhraseToken.Kilometers_2_4, "Kilometers2_4" },
            { PhraseToken.Meter, "Meter" },
            { PhraseToken.Meters, "Meters" },
            { PhraseToken.Meters_2_4, "Meters2_4" },
            { PhraseToken.Then, "Then" },
            { PhraseToken.Over, "Over" },
            { PhraseToken.Ahead, "Ahead" },
            { PhraseToken.Straight, "Forward" },
            { PhraseToken.EnterRoundabout, "InCircularMovement" },
            { PhraseToken.RouteWillFinish, "RouteWillFinish" },
            { PhraseToken.RouteFinished, "RouteFinished" },
            { PhraseToken.HardTurnLeft, "HardTurnLeft" },
            { PhraseToken.HardTurnRight, "HardTurnRight" },
            { PhraseToken.TakeLeft, "TakeLeft" },
            { PhraseToken.TakeRight, "TakeRight" },
            { PhraseToken.TurnBack, "TurnBack" },
            { PhraseToken.TurnLeft, "TurnLeft" },
            { PhraseToken.TurnRight, "TurnRight" },
            { PhraseToken.BoardFerry, "BoardFerry" },
            { PhraseToken.Exit, "Exit" },
            { PhraseToken.AfterBridge, "LandmarkAfterBridge" },
            { PhraseToken.AfterTunnel, "LandmarkAfterTunnel" },
            { PhraseToken.AtTrafficLights, "LandmarkAtTrafficLights" },
            { PhraseToken.BeforeBridge, "LandmarkBeforeBridge" },
            { PhraseToken.BeforeTrafficLights, "LandmarkBeforeTrafficLights" },
            { PhraseToken.BeforeTunnel, "LandmarkBeforeTunnel" },
            { PhraseToken.IntoCourtyard, "LandmarkIntoCourtyard" },
            { PhraseToken.IntoTunnel, "LandmarkIntoTunnel" },
            { PhraseToken.ToBridge, "LandmarkToBridge" },
            { PhraseToken.ToFrontageRoad, "LandmarkToFrontageRoad" },
            { PhraseToken.AtLeft, "AtLeft" },
            { PhraseToken.AtRight, "AtRight" },
            { PhraseToken.AtMiddle, "AtMiddle" },
            { PhraseToken.AndRight, "AndRight" },
            { PhraseToken.AndMiddle, "AndMiddle" },
            { PhraseToken.LaneLocative, "Row" },
            { PhraseToken.SpeedCamera, "SpeedCamera" },
            { PhraseToken.SpeedLimitCamera, "SpeedCamera" },
            { PhraseToken.LaneCamera, "LaneCamera" },
            { PhraseToken.Speed30, "Speed30" },
            { PhraseToken.Speed40, "Speed40" },
            { PhraseToken.Speed50, "Speed50" },
            { PhraseToken.Speed60, "Speed60" },
            { PhraseToken.Speed70, "Speed70" },
            { PhraseToken.Speed80, "Speed80" },
            { PhraseToken.Speed90, "Speed90" },
            { PhraseToken.Speed100, "Speed100" },
            { PhraseToken.Speed110, "Speed110" },
            { PhraseToken.Speed120, "Speed120" },
            { PhraseToken.Speed130, "Speed130" },
            { PhraseToken.Accident, "Accident" },
            { PhraseToken.Reconstruction, "Reconstruction" },
            { PhraseToken.RouteUpdated, "RouteRecalculated" },
            { PhraseToken.GoneOffRoute, "RouteLost" },
            { PhraseToken.SpeedLimitExceeded, "Danger" },
            { PhraseToken.ReturnedOnRoute, "RouteReturn" },
            { PhraseToken.ViaPointPassed, "RouteViaPoint" },
            { PhraseToken.FasterRouteAvailable, string.Empty },
            { PhraseToken.Roundabout, "InCircularMovement" },
            { PhraseToken.LanesLocative, "Row" },
            { PhraseToken.DoExit, "Exit" },
            { PhraseToken.Attention, "Danger" },
            { PhraseToken.GetLeft, "TakeLeft" },
            { PhraseToken.GetRight, "TakeRight" }
        };

        private readonly PhraseToken[] _commonTokens = { PhraseToken.Attention, PhraseToken.SpeedLimitExceeded };

        private readonly Dictionary<Tuple<string, string>, double> _durations = new Dictionary<Tuple<string, string>, double>();

        private readonly ILogger _logger;

        #endregion

        #region Constructors

        public VoiceFileHelper(ILogManager logManager)
        {
            _logger = logManager.GetLogger(Scopes.Driving, LogManagerFactory.DefaultConfiguration);
        }

        #endregion

        #region Static Methods

        [CanBeNull]
        private static string GetFileName(PhraseToken token)
        {
            string fileName;
            _fileNames.TryGetValue(token, out fileName);
            return fileName;
        }

        private static string ToLocale(AnnotationLanguage language)
        {
            switch (language)
            {
                case AnnotationLanguage.Russian:
                case AnnotationLanguage.Turkish:
                case AnnotationLanguage.Ukrainian: return language.ToString();
                default: return AnnotationLanguage.English.ToString();
            }
        }

        #endregion

        #region Methods

        public double GetDuration(LocalizedPhrase phrase, Sex speakerSex)
        {
            return Enumerable.Range(0, Convert.ToInt32(phrase.TokensCount()))
                       .Select(Convert.ToUInt32)
                       .Select(phrase.Token)
                       .Select(token => GetTokenDuration(token, phrase.Language(), speakerSex))
                       .Sum() + 1.0; //add secod like in IOS
        }

        [CanBeNull]
        public Uri GetFileUri(PhraseToken token, AnnotationLanguage language, Sex speakerSex)
        {
            string fileName = GetFileName(token);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.Warn($"No file for {token} {language}");
                return null;
            }
            return _commonTokens.Contains(token)
                ? new Uri($"ms-appx:///Resources/Sounds/Guidance/Common/{fileName}.mp3")
                : new Uri($"ms-appx:///Resources/Sounds/Guidance/{ToLocale(language)}/{speakerSex.ToString().ToLower()}/{fileName}.mp3");
        }

        public async Task InitDurations()
        {
            if (_durations.Any())
                return;
            var localeKeys = new[]
            {
                $"{AnnotationLanguage.Russian}/male", $"{AnnotationLanguage.Ukrainian}/male", /* $"{AnnotationLanguage.English}/male",*/ $"{AnnotationLanguage.Turkish}/male",
                $"{AnnotationLanguage.Russian}/female", $"{AnnotationLanguage.Ukrainian}/female", $"{AnnotationLanguage.English}/female", $"{AnnotationLanguage.Turkish}/female",
                "Common"
            };
            foreach (string localeKey in localeKeys)
            {
                try
                {
                    var durationsFileUri = new Uri($"ms-appx:///Resources/Sounds/Guidance/{localeKey}/durations.plist");

                    StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(durationsFileUri);
                    IRandomAccessStreamWithContentType randomAccessStream = await file.OpenReadAsync();
                    Stream stream = randomAccessStream.AsStreamForRead();

                    XDocument document = XDocument.Load(stream);
                    XElement dict = document.Root.Element("dict");
                    IEnumerable<string> keys = dict.Elements("key").Select(e => e.Value);
                    IEnumerable<double> durations = dict.Elements("real").Select(e => double.Parse(e.Value, CultureInfo.InvariantCulture));
                    var pairs = keys.Zip(durations, (key, duration) => new { key, duration });

                    foreach (var pair in pairs)
                    {
                        Tuple<string, string> tuple = Tuple.Create(localeKey, pair.key);
                        _durations[tuple] = pair.duration;
                    }
                }
                catch (Exception exception)
                {
                    _logger.Error("InitDurations", exception);
                }
            }
        }

        private double GetTokenDuration(PhraseToken token, AnnotationLanguage language, Sex speakerSex)
        {
            string localeKey = _commonTokens.Contains(token) ? "Common" : $"{ToLocale(language)}/{speakerSex.ToString().ToLower()}";
            string key = GetFileName(token);

            double duration;
            if (!_durations.TryGetValue(Tuple.Create(localeKey, key), out duration))
            {
                _logger.Warn($"No duration for {token} {language}");
            }
            return duration;
        }

        #endregion
    }
}
