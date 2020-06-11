using System;
using System.Threading.Tasks;
using Company.App.Analytics;
using Company.App.Services.Authentification.Data;
using Company.App.Services.Contracts.Analytics;
using Company.App.Utils.Union;
using Company.Runtime.Auth;

namespace Company.App.Services.Authentification
{
    internal class RuntimeAccount : Account
    {
        #region Static and Readonly Fields

        private readonly IInternalAuthService _internalAuthService;

        private readonly UserInfo _userInfo;

        #endregion

        #region Fields

        private Task<Union3<UserInfo, LoginErrorDto, Exception>> _loginTask;

        #endregion

        #region Constructors

        public RuntimeAccount(UserInfo userInfo, IInternalAuthService internalAuthService)
        {
            _userInfo = userInfo;
            _internalAuthService = internalAuthService;
        }

        #endregion

        #region Methods

        private async Task TryGetToken(TokenListener tokenListener)
        {
            _loginTask = _loginTask ?? _internalAuthService.LoginWithXToken(_userInfo.Xtoken);

            Union3<UserInfo, LoginErrorDto, Exception> loginResult = await _loginTask;
            loginResult.Match(userInfo =>
            {
                _userInfo.Token = userInfo.Token;
                _userInfo.Uid = userInfo.Uid;
                tokenListener.OnTokenReceived(_userInfo.Token);
            },
                error =>
                {
                    tokenListener.OnTokenRefreshFailed(error.Error);
                    AppAnalytics.Instance.TrackError(error.ErrorDescription);
                },
                ex =>
                {
                    tokenListener.OnTokenRefreshFailed(ex.Message);
                    AppAnalytics.Instance.TrackError(ex);
                });

            _loginTask = null;
        }

        #endregion

        #region Account Members

        public void InvalidateToken(string token) => _userInfo.Token = null;

        public string HttpAuth(string token) => $"OAuth {token}";

        public async void RequestToken(TokenListener tokenListener)
        {
            if (!string.IsNullOrWhiteSpace(_userInfo.Token))
            {
                tokenListener.OnTokenReceived(_userInfo.Token);
            }
            else
            {
                await TryGetToken(tokenListener);
            }
        }

        public string Uid() => _userInfo.Uid;

        #endregion
    }
}
