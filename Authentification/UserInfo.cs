using System.Threading.Tasks;
using JetBrains.Annotations;
using Company.App.Services.Contracts.Authentification;
using Company.App.Utils.Extensions;

namespace Company.App.Services.Authentification
{
    public class UserInfo : IUserInfo
    {
        #region Constructors

        public UserInfo(string fullName, string email, string avatarId, string uid, string token, string xtoken)
        {
            FullName = fullName;
            Email = email;
            AvatarId = avatarId;
            Uid = uid;
            Token = token;
            Xtoken = xtoken;
        }

        #endregion

        #region Properties

        public string AvatarId { get; }
        public string Token { get; set; }
        public string Xtoken { get; }

        #endregion

        #region Methods

        public override string ToString() => $"User info: {FullName} {Email} {Uid} *{Token.LastSymbols(4)} *{Xtoken.LastSymbols(4)}";

        #endregion

        #region IUserInfo Members

        [ItemCanBeNull]
        public Task<string> AvatarUriTask { get; set; }

        public string Email { get; }
        public string FullName { get; }
        public string Uid { get; set; }

        #endregion
    }
}
