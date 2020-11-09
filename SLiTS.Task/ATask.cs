using NLog;
using System;
using System.Threading.Tasks;

namespace SLiTS.Api
{
    public abstract class ATask
    {
        public ATask(ILogger logger)
        {
            Logger = logger;
        }
        public string Title { get; set; }
        protected ILogger Logger { get; }
        public string Params { get; set; }
        public async Task InvokeAsync(Action continueWith)
        {
            await InternalInvokeAsync();
            continueWith();
        }
        protected abstract Task InternalInvokeAsync();
        public abstract Task<bool> Test();
    }
}
