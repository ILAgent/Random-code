using System;
using System.Threading.Tasks;
using MetroLog;
using Company.System;
using Company.App.Services.Authentification.Data;
using Company.App.Services.Contracts.Authentification;
using Company.App.Services.Contracts.Logging;
using Company.App.Services.Contracts.UserData;
using Company.App.Services.Sync;
using Company.App.Services.UserData;
using Company.App.Utils.Logging;
using Company.App.Utils.Union;
using UserInfoUnion =
    Company.App.Utils.Union.Union4
        <Company.App.Services.Contracts.Authentification.IUserInfo, Company.App.Services.Contracts.Authentification.LoginError, System.Exception,
            Company.App.Services.Contracts.Authentification.CaptchaQuery>;

namespace Company.App.Services.Authentification
{
    internal class AuthService : IAuthService
    {
        #region Static and Readonly Fields

        private readonly AvatarSaver _avatarSaver;
        private readonly IInternalAuthService _internalAuthService;
        private readonly ILogger _logger;
        private readonly ISyncManager _syncManager;
        private readonly IUserDataService _userDataService;
        private readonly IAccountRepository _accountRepository;

        #endregion

        #region Constructors

        public AuthService(IInternalAuthService internalAuthService,
            ISyncManager syncManager,
            IUserDataService userDataService,
            AvatarSaver avatarSaver,
            ILogManager logManager, IAccountRepository accountRepository)
        {
            _internalAuthService = internalAuthService;
            _syncManager = syncManager;
            _userDataService = userDataService;
            _avatarSaver = avatarSaver;
            _accountRepository = accountRepository;
            _logger = logManager.GetLogger(Scopes.Login, LogManagerFactory.DefaultConfiguration);
        }

        #endregion

        #region Methods

        private void ProcessLocalStoredUserInfo(UserInfo userInfo)
        {
            userInfo.AvatarUriTask = Task.FromResult(AvatarSaver.AvatarFilePath);
            var runtimeAccount = new RuntimeAccount(userInfo, _internalAuthService);
            SystemFactory.Instance.SetAccount(runtimeAccount);
            _syncManager.OpenDatabases(runtimeAccount);
        }

        private void ProcessUserInfo(UserInfo userInfo)
        {
            _logger.Method().Start(userInfo);

            userInfo.AvatarUriTask = _avatarSaver.SaveAvatar(userInfo.AvatarId);

            _accountRepository.AccountData = new AccountData(userInfo);
            _userDataService.SaveUserData();

            var runtimeAccount = new RuntimeAccount(userInfo, _internalAuthService);
            SystemFactory.Instance.SetAccount(runtimeAccount);
            _syncManager.OpenDatabases(runtimeAccount);

            _logger.Method().End();
        }

        #endregion

        #region IAuthService Members

        public IUserInfo GetLocalStoredUserInfo()
        {
            _logger.Method().Start();

            AccountData acData = _accountRepository.AccountData;
            if (acData == null)
                return (null as IUserInfo).RetWithLog(_logger).Log();
            var userInfo = new UserInfo(acData.FullName, acData.Email, null, acData.Uid, acData.OauthToken, acData.XToken);
            ProcessLocalStoredUserInfo(userInfo);
            return userInfo.RetWithLog(_logger).Log();
        }

        public async Task<IUserInfo> GetUserInfo()
        {
            _logger.Method().Start();

            AccountData acData = _accountRepository.AccountData;

            Union3<UserInfo, LoginErrorDto, Exception> loginResult;
            if (!string.IsNullOrWhiteSpace(acData?.OauthToken) && !string.IsNullOrWhiteSpace(acData.XToken))
            {
                loginResult = await _internalAuthService.GetUserInfo(acData.OauthToken, acData.XToken);
            }
            else if (!string.IsNullOrWhiteSpace(acData?.XToken))
            {
                loginResult = await _internalAuthService.LoginWithXToken(acData.XToken);
            }
            else
            {
                return (null as IUserInfo).RetWithLog(_logger).Log();
            }

            return loginResult.Match(ui =>
            {
                ProcessUserInfo(ui);
                return ui;
            },
                _ => null,
                _ => null).RetWithLog(_logger).Log();
        }

        public async Task<UserInfoUnion> Login(string userName, string password, CaptchaAnswer captchaAnswer = null)
        {
            _logger.Method().Start($"{userName} | {captchaAnswer}");

            Union3<UserInfo, LoginErrorDto, Exception> userInfoUnion = await _internalAuthService.Login(userName, password, captchaAnswer);
            return userInfoUnion.Match(userInfo =>
            {
                ProcessUserInfo(userInfo);
                return new UserInfoUnion.Case1(userInfo.RetWithLog(_logger).Log());
            },
                error =>
                    string.IsNullOrWhiteSpace(error.CaptchaKey) || string.IsNullOrWhiteSpace(error.CaptchaUrl)
                        ? (UserInfoUnion)new UserInfoUnion.Case2(new LoginError(error.Error, error.ErrorDescription).RetWithLog(_logger).Log())
                        : new UserInfoUnion.Case4(new CaptchaQuery(error.CaptchaKey, error.CaptchaUrl).RetWithLog(_logger).Log()),
                ex => new UserInfoUnion.Case3(ex.RetWithLog(_logger).Log()));
        }

        public async void Logoff()
        {
            _logger.Method().Start();

            _userDataService.Logoff();
            SystemFactory.Instance.SetAccount(null);
            _syncManager.OpenDatabases(null);
            await _avatarSaver.DeleteAvatar();

            _logger.Method().End();
        }

        #endregion
    }
}
