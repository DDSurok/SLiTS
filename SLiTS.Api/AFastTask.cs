using NLog;
using System.Threading.Tasks;

namespace SLiTS.Api
{
    public abstract class AFastTask
    {
        public AFastTask(ILogger logger,
                         IAsyncStatisticIntercepter<AFastTask> statisticIntercepter,
                         ISharedPropertyProvider propertyProvider)
            => (Logger, StatisticIntercepter, PropertyProvider) = (logger, statisticIntercepter, propertyProvider);
        public virtual string Parameters { get; set; }
        public abstract string Title { get; }
        public ILogger Logger { get; }
        public IAsyncStatisticIntercepter<AFastTask> StatisticIntercepter { get; }
        protected ISharedPropertyProvider PropertyProvider { get; }

        public abstract Task<FastTaskResponse> InvokeAsync(FastTaskRequest request);
    }
}
