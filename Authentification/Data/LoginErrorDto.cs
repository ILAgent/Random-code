using Newtonsoft.Json;

namespace Company.App.Services.Authentification.Data
{
    public class LoginErrorDto
    {
        #region Properties

        [JsonProperty("x_captcha_key")]
        public string CaptchaKey { get; set; }

        [JsonProperty("x_captcha_url")]
        public string CaptchaUrl { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("error_description")]
        public string ErrorDescription { get; set; }

        #endregion
    }
}
