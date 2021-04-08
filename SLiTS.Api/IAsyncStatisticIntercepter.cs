using System;
using System.Threading.Tasks;

namespace SLiTS.Api
{
    public interface IAsyncStatisticIntercepter<out T> where T : AQuickTask
    {
        int Counter { get; }
        int Limit { get; }
        Task RegistredFinalAsync(string query, DateTime start, TimeSpan delay, int dataCount);
        Task RegistredFinalAsync(ImplementationRecord record);
        StatisticRecord Statistic { get; }
    }
}