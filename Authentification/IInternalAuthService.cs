using System;
using System.Threading.Tasks;
using Company.App.Services.Authentification.Data;
using Company.App.Services.Contracts.Authentification;
using Company.App.Utils.Union;

namespace Company.App.Services.Authentification
{
    internal interface IInternalAuthService
    {
        #region Methods

        Task<Union3<UserInfo, LoginErrorDto, Exception>> Login(string userName, string password, CaptchaAnswer captchaAnswer = null);

        Task<Union3<UserInfo, LoginErrorDto, Exception>> GetUserInfo(string oauthToken, string xtoken);

        Task<Union3<UserInfo, LoginErrorDto, Exception>> LoginWithXToken(string xtoken);

        #endregion
    }
}
