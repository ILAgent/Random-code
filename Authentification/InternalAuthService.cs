using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using MetroLog;
using Newtonsoft.Json;
using Company.App.Analytics;
using Company.App.Common;
using Company.App.Services.Authentification.Data;
using Company.App.Services.Contracts.Analytics;
using Company.App.Services.Contracts.Authentification;
using Company.App.Services.Contracts.Logging;
using Company.App.Utils;
using Company.App.Utils.Extensions;
using Company.App.Utils.Logging;
using UserInfoUnion = Company.App.Utils.Union.Union3<Company.App.Services.Authentification.UserInfo, Company.App.Services.Authentification.Data.LoginErrorDto, System.Exception>;

namespace Company.App.Services.Authentification
{
    internal class InternalAuthService : IInternalAuthService
    {
        #region Constants

        //music
        //private const string ClientId = "23cabbbdc6cd418abb4b39c32c41195d";
        //private const string ClientSecret = "53bc75238f0c4d08a118e51fe9203300";

        private const string OAuthUrl = "https://oauth.Company.ru/token";

        #endregion

        #region Static and Readonly Fields

        private static readonly string _amClientId = SymmetricEncryption.Decrypt("wqrtgSlO3CeHH0gOHhzA2ifPlDEl2YY75VXMM1RsfEvUqFVPW3+ke3YrHErv7D9A");
        //"c0ebe342af7d48fbbbfcf2d2eedb8f9e";

        private static readonly string _amClientSecret = SymmetricEncryption.Decrypt("FBT4+SIKJkmyQhR6mu39egAo+gV3AU9p1624CsB6OEzUqFVPW3+ke3YrHErv7D9A");
        // "ad0a908f0aa341a182a37ecd75bc319e";

        //App
        private static readonly string _clientId = SymmetricEncryption.Decrypt("1LAOhIp85yMzZ0pLYtrrOtJlTe5ssZbHPAyIPIdGrlHUqFVPW3+ke3YrHErv7D9A");
        // "85e536eea2f345a3ad394c1b1f01df6b";

        private static readonly string _clientSecret = SymmetricEncryption.Decrypt("2nBvar5QSv3ayoBHmsxO0Hn6ELYwnweIac/I/AKr+oPUqFVPW3+ke3YrHErv7D9A");

        private readonly ILogger _logger;

        #endregion

        #region Constructors

        public InternalAuthService(ILogManager logManager)
        {
            _logger = logManager.GetLogger(Scopes.Login, LogManagerFactory.DefaultConfiguration);
        }

        #endregion

        #region Methods

        private Option<LoginErrorDto> DeserializeLoginError(string json)
        {
            _logger.Method().Start(json);

            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    LoginErrorDto dto = JsonConvert.DeserializeObject<LoginErrorDto>(json).RetWithLog(_logger).Log(d => d.ErrorDescription);

                    return Prelude.Some(dto);
                }
            }
            catch (Exception ex)
            {
                AppAnalytics.Instance.TrackError(ex);
            }

            return LoggerExtensions.RetWithLog(Prelude.None, _logger).Log();
        }

        private async Task<UserInfoUnion> DoWithExceptionHandling(Task<UserInfo> userInfoTask)
        {
            _logger.Method().Start();
            try
            {
                return new UserInfoUnion.Case1((await userInfoTask).RetWithLog(_logger).Log());
            }
            catch (WebException exception)
            {
                if (exception.Response == null)
                {
                    AppAnalytics.Instance.TrackError(exception);
                    return new UserInfoUnion.Case3(exception.RetWithLog(_logger).Log());
                }

                using (var streamReader = new StreamReader(exception.Response.GetResponseStream()))
                {
                    string responseContent = streamReader.ReadToEnd();
                    Option<LoginErrorDto> loginError = DeserializeLoginError(responseContent);
                    return loginError.Match(error =>
                    {
                        AppAnalytics.Instance.TrackError($"{error.Error} : {error.ErrorDescription}");
                        return (UserInfoUnion)new UserInfoUnion.Case2(error.RetWithLog(_logger).Log());
                    },
                        () =>
                        {
                            AppAnalytics.Instance.TrackError(exception);
                            return new UserInfoUnion.Case3(exception.RetWithLog(_logger).Log());
                        });
                }
            }
            catch (Exception ex)
            {
                AppAnalytics.Instance.TrackError(ex);
                return new UserInfoUnion.Case3(ex.RetWithLog(_logger).Log());
            }
        }

        private async Task<UserInfo> InternalGetUserInfo(string oauthToken, string xtoken)
        {
            _logger.Method().Start();

            string url = $"https://login.Company.ru/info?oauth_token={oauthToken}";
            var request = (HttpWebRequest)WebRequest.Create(url);

            var response = (HttpWebResponse)await request.GetResponseAsync();
            using (var streamReader = new StreamReader(response.GetResponseStream()))
            {
                string responseContent = streamReader.ReadToEnd();
                var result = JsonConvert.DeserializeObject<LoginResult>(responseContent);

                return new UserInfo(result.RealName, result.DefaultEmail, result.DefaultAvatarId, result.Id, oauthToken, xtoken).RetWithLog(_logger).Log();
            }
        }

        private async Task<OAuthResult> OAuthRequest(Dictionary<string, string> postData)
        {
            _logger.Method().Start();

            string body = string.Join("&", postData.Select(kvp => string.Format("{0}={1}", WebUtility.UrlEncode(kvp.Key), WebUtility.UrlEncode(kvp.Value))));

            var request = (HttpWebRequest)WebRequest.Create(OAuthUrl);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Accept = "application/json";

            using (Stream requestStream = await request.GetRequestStreamAsync())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                requestStream.Write(bytes, 0, bytes.Length);
            }

            var response = (HttpWebResponse)await request.GetResponseAsync();

            using (var streamReader = new StreamReader(response.GetResponseStream()))
            {
                string responseContent = streamReader.ReadToEnd();
                var result = JsonConvert.DeserializeObject<OAuthResult>(responseContent);
                return result.RetWithLog(_logger).Log();
            }
        }

        private async Task<UserInfo> UnsafeLogin(string userName, string password, CaptchaAnswer captchaAnswer)
        {
            _logger.Method().Start($"{userName} {captchaAnswer}");

            var passwordtData = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", _amClientId },
                { "client_secret", _amClientSecret },
                { "username", userName },
                { "password", password }
            };

            if (captchaAnswer != null)
            {
                passwordtData["x_captcha_key"] = captchaAnswer.Key;
                passwordtData["x_captcha_answer"] = captchaAnswer.Answer;
            }

            OAuthResult resultWithXToken = await OAuthRequest(passwordtData);

            return (await UnsafeLoginWithXToken(resultWithXToken.AccessToken)).RetWithLog(_logger).Log();
        }

        private async Task<UserInfo> UnsafeLoginWithXToken(string xToken)
        {
            _logger.Method().Start(xToken.LastSymbols(4));

            var xTokenData = new Dictionary<string, string>
            {
                { "grant_type", "x-token" },
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "access_token", xToken }
            };

            OAuthResult resultWithOauthToken = await OAuthRequest(xTokenData);

            UserInfo userInfo = await InternalGetUserInfo(resultWithOauthToken.AccessToken, xToken);

            return userInfo.RetWithLog(_logger).Log();
        }

        #endregion

        #region IInternalAuthService Members

        public async Task<UserInfoUnion> GetUserInfo(string oauthToken, string xtoken)
        {
            _logger.Method().Start();
            try
            {
                string url = $"https://login.Company.ru/info?oauth_token={oauthToken}";
                var request = (HttpWebRequest)WebRequest.Create(url);

                var response = (HttpWebResponse)await request.GetResponseAsync();
                using (var streamReader = new StreamReader(response.GetResponseStream()))
                {
                    string responseContent = streamReader.ReadToEnd();
                    var result = JsonConvert.DeserializeObject<LoginResult>(responseContent);

                    return
                        new UserInfoUnion.Case1(new UserInfo(result.RealName, result.DefaultEmail, result.DefaultAvatarId, result.Id, oauthToken, xtoken).RetWithLog(_logger).Log());
                }
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.ProtocolError)
            {
                AppAnalytics.Instance.TrackError("GetUserInfo", ex);

                var response = ex.Response as HttpWebResponse;
                return response != null && (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Unauthorized)
                    ? await LoginWithXToken(xtoken)
                    : new UserInfoUnion.Case3(ex.RetWithLog(_logger).Log());
            }
            catch (Exception ex)
            {
                AppAnalytics.Instance.TrackError("GetUserInfo", ex);
                return new UserInfoUnion.Case3(ex.RetWithLog(_logger).Log());
            }
        }

        public Task<UserInfoUnion> Login(string userName, string password, CaptchaAnswer captchaAnswer = null)
            => DoWithExceptionHandling(UnsafeLogin(userName, password, captchaAnswer));

        public Task<UserInfoUnion> LoginWithXToken(string xtoken) => DoWithExceptionHandling(UnsafeLoginWithXToken(xtoken));

        #endregion

        //"814df3e0b96c4a3b8f69e00865fdd596";
    }
}
