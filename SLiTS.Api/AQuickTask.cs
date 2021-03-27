using NLog;
using System.Threading.Tasks;

namespace SLiTS.Api
{
    public abstract class AQuickTask
    {
        public AQuickTask(ILogger logger,
                         IAsyncStatisticIntercepter<AQuickTask> statisticIntercepter,
                         ISharedPropertyProvider propertyProvider)
            => (Logger, StatisticIntercepter, PropertyProvider) = (logger, statisticIntercepter, propertyProvider);
        public virtual string Parameters { get; set; }
        public abstract string Title { get; }
        public ILogger Logger { get; }
        public IAsyncStatisticIntercepter<AQuickTask> StatisticIntercepter { get; }
        protected ISharedPropertyProvider PropertyProvider { get; }
        public abstract Task<QuickTaskResponse> InvokeAsync(QuickTaskRequest request);
    }
}
