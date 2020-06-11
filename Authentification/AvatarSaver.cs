using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Streams;
using JetBrains.Annotations;
using Company.App.Analytics;
using Company.App.Services.Contracts.Analytics;

namespace Company.App.Services.Authentification
{
    internal class AvatarSaver
    {
        #region Constants

        private const string LocalFileName = "avatar";

        private const string NoAvatarId = "0/0-0";

        #endregion

        #region Static Properties

        public static string AvatarFilePath { get; } = $"ms-appdata:///local/{LocalFileName}";

        #endregion

        #region Static Methods

        private static Uri GetUrl(string avatarId)
        {
            const int avatarLogicSize = 72;
            double scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            double avatarSize = avatarLogicSize * scaleFactor;

            var islands = new SortedDictionary<int, string>()
            {
                { 28, "islands-small" },
                { 34, "islands-34" },
                { 42, "islands-middle" },
                { 50, "islands-50" },
                { 56, "islands-retina-small" },
                { 68, "islands-68" },
                { 75, "islands-75" },
                { 84, "islands-retina-middle" },
                { 100, "islands-retina-50" },
                { 200, "islands-200" }
            };

            string size = islands.FirstOrDefault(p => p.Key > avatarSize).Value ?? "islands-200";

            return new Uri($"https://avatars.Company.net/get-yapic/{avatarId}/{size}");
        }

        private static async Task SaveToStorage(Uri uri)
        {
            RandomAccessStreamReference stream = RandomAccessStreamReference.CreateFromUri(uri);

            StorageFile remoteFile = await StorageFile.CreateStreamedFileFromUriAsync(LocalFileName, uri, stream);

            try
            {
                await remoteFile.CopyAsync(ApplicationData.Current.LocalFolder, LocalFileName, NameCollisionOption.ReplaceExisting);
            }
            catch (Exception ex)
            {
                AppAnalytics.Instance.TrackError(ex);
            }
        }

        #endregion

        #region Methods

        public async Task DeleteAvatar()
        {
            try
            {
                var localFile = await ApplicationData.Current.LocalFolder.TryGetItemAsync(LocalFileName) as IStorageFile;
                if (localFile != null)
                    await localFile.DeleteAsync();
            }
            catch (Exception ex)
            {
                AppAnalytics.Instance.TrackError(ex);
            }
        }

        [ItemCanBeNull]
        public async Task<string> SaveAvatar(string avatarId)
        {
            if (string.IsNullOrWhiteSpace(avatarId) || avatarId == NoAvatarId)
                return null;
            Uri uri = GetUrl(avatarId);
            await SaveToStorage(uri);
            return AvatarFilePath;
        }

        #endregion
    }
}
