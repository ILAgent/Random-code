using Newtonsoft.Json;
using Company.App.Utils.Extensions;

namespace Company.App.Services.Authentification.Data
{
    public class OAuthResult
    {
        #region Properties

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        #endregion

        #region Methods

        public override string ToString()
        {
            return $"OAuthResult: *{AccessToken.LastSymbols(4)} {TokenType} {Error} {ErrorDescription}";
        }

        #endregion
    }
}
