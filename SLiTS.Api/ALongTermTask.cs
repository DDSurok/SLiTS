using NLog;
using System.Threading;
using System.Threading.Tasks;

namespace SLiTS.Api
{
    public abstract class ALongTermTask
    {
        public ALongTermTask(ILogger logger, ISharedPropertyProvider propertyProvider)
            => (Logger, PropertyProvider) = (logger, propertyProvider);
        public string Title { get; set; }
        protected ILogger Logger { get; }
        protected ISharedPropertyProvider PropertyProvider { get; }
        public string Params { get; set; }
        public abstract Task InvokeAsync(CancellationToken token);
        public abstract Task<bool> TestAsync();
    }
}
