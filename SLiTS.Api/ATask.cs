using NLog;
using System.Threading;
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
        public abstract Task InvokeAsync(CancellationToken token);
        public abstract Task<bool> TestAsync();
    }
}
