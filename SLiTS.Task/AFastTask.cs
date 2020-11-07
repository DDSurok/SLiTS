using NLog;
using System.Threading.Tasks;

namespace SLiTS.Api
{
    public abstract class AFastTask
    {
        public AFastTask(ILogger logger, IAsyncStatisticIntercepter<AFastTask> statisticIntercepter)
            => (Logger, StatisticIntercepter) = (logger, statisticIntercepter);
        public string Params { get; set; }
        public abstract string Title { get; }
        public ILogger Logger { get; }
        public IAsyncStatisticIntercepter<AFastTask> StatisticIntercepter { get; }
        public abstract Task<Data> InvokeAsync(string query);
    }
}
