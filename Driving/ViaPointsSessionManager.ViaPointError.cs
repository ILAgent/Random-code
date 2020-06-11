using System;
using System.Threading.Tasks;
using Company.App.Services.Contracts.Guidance.Driving;

namespace Company.App.Services.Guidance.Driving
{
    internal partial class ViaPointsSessionManager
    {
        #region Nested type: ViaPointError

        private class ViaPointError : IViaPointError
        {
            #region Static and Readonly Fields

            private readonly Func<Task> _retryFunc;

            #endregion

            #region Constructors

            public ViaPointError(Func<Task> retryFunc)
            {
                _retryFunc = retryFunc;
            }

            #endregion

            #region IViaPointError Members

            public Task Retry() => _retryFunc();

            #endregion
        }

        #endregion
    }
}
