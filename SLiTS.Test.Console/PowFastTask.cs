using NLog;
using SLiTS.Api;
using System;
using System.Threading.Tasks;

namespace SLiTS.Test
{
    public class PowFastTask : AFastTask
    {
        public PowFastTask(ILogger logger, IAsyncStatisticIntercepter<AFastTask> statisticIntercepter)
            : base(logger, statisticIntercepter) { }

        public override string Title => "Test fast task digit pow 2";

        public override async Task<FastTaskResponse> InvokeAsync(FastTaskRequest request)
        {
            DateTime start = DateTime.Now;
            await Task.Delay(new Random((int)start.Ticks).Next(100, 5000));
            int input = int.Parse(request.Query);
            await StatisticIntercepter.RegistredFinalAsync(new ImplementationRecord
            {
                Start = start,
                DataCount = 1,
                Delay = DateTime.Now - start,
                Query = request.Query
            });
            return new FastTaskResponse { Id = request.Id, Metadata = "", Data = Math.Pow(input, 2).ToString() };
        }
    }
}