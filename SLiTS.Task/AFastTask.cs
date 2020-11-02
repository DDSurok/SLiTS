using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace SLiTS.Api
{
    public abstract class AFastTask
    {
        public AFastTask(string @params, ILogger logger, IAsyncStatisticIntercepter<AFastTask> statisticIntercepter)
        {
            Params = @params;
            Logger = logger;
            StatisticIntercepter = statisticIntercepter;
        }
        public string Params { get; }
        public abstract string Title { get; }
        public ILogger Logger { get; }
        public IAsyncStatisticIntercepter<AFastTask> StatisticIntercepter { get; }

        public abstract Task Invoke(string query);
    }
}
