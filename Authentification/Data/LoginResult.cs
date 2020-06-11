using System.Collections.Generic;
using Newtonsoft.Json;

namespace Company.App.Services.Authentification.Data
{
    public class LoginResult
    {
        #region Properties

        [JsonProperty("birthday")]
        public string Birthday { get; set; }

        [JsonProperty("default_avatar_id")]
        public string DefaultAvatarId { get; set; }

        [JsonProperty("default_email")]
        public string DefaultEmail { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("emails")]
        public IList<string> Emails { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("is_avatar_empty")]
        public bool IsAvatarEmpty { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        [JsonProperty("login")]
        public string Login { get; set; }

        [JsonProperty("old_social_login")]
        public string OldSocialLogin { get; set; }

        [JsonProperty("openid_identities")]
        public IList<string> OpenidIdentities { get; set; }

        [JsonProperty("real_name")]
        public string RealName { get; set; }

        [JsonProperty("sex")]
        public string Sex { get; set; }

        #endregion
    }
}
