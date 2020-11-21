using NLog;
using SLiTS.Api;
using System;
using System.Threading.Tasks;

namespace SLiTS.Test.Console
{
    public class PowFastTask : AFastTask
    {
        public PowFastTask(ILogger logger, IAsyncStatisticIntercepter<AFastTask> statisticIntercepter)
            : base(logger, statisticIntercepter) { }

        public override string Title => $"Test fast task digit pow {PowB}";

        public int PowB { get; set; }

        public override string Parameters { get => PowB.ToString(); set => PowB = int.Parse(value); }

        public override async Task<FastTaskResponse> InvokeAsync(FastTaskRequest request)
        {
            DateTime start = DateTime.Now;
            int input = int.Parse(request.Query);
            await Task.Delay(new Random((int)start.Ticks).Next(100, 5000));
            int result = (int)Math.Pow(input, PowB);
            await StatisticIntercepter.RegistredFinalAsync(new ImplementationRecord
            {
                Start = start,
                DataCount = 1,
                Delay = DateTime.Now - start,
                Query = request.Query
            });
            return new FastTaskResponse { Id = request.Id, Metadata = "", Data = result.ToString() };
        }
    }
}