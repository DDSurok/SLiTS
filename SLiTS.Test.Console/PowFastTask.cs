using NLog;
using SLiTS.Api;
using System;
using System.Threading.Tasks;

namespace SLiTS.Test.Console
{
    public class PowFastTask : AQuickTask
    {
        public PowFastTask(ILogger logger, IAsyncStatisticIntercepter<AQuickTask> statisticIntercepter, ISharedPropertyProvider propertyProvider)
            : base(logger, statisticIntercepter, propertyProvider) { }

        public override string Title => $"Test fast task digit pow {PowB}";

        public int PowB { get; set; }

        public override string Parameters { get => PowB.ToString(); set => PowB = int.Parse(value); }

        public override async Task<QuickTaskResponse> InvokeAsync(QuickTaskRequest request)
        {
            DateTime start = DateTime.Now;
            int input = int.Parse(request.Query);
            await Task.Delay(new Random((int)start.Ticks).Next(100, 1000));
            int result = (int)Math.Pow(input, PowB);
            await StatisticIntercepter.RegistredFinalAsync(new ImplementationRecord
            {
                Start = start,
                DataCount = 1,
                Delay = DateTime.Now - start,
                Query = request.Query
            });
            return new QuickTaskResponse { Id = request.Id, Metadata = "", Data = result.ToString() };
        }
    }
}