using SLiTS.Api;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SLiTS.Scheduler
{
    public class StatisticIntercepter<T> : IAsyncStatisticIntercepter<T> where T : AQuickTask
    {
        public int Counter { get; private set; } = 0;
        public int Limit { get; }
        public StatisticIntercepter()
        {
            Limit = 10;
            History = new LimitedConcurrentQueue<ImplementationRecord>(Limit);
        }
        public LimitedConcurrentQueue<ImplementationRecord> History { get; }
        public StatisticRecord Statistic
            => Counter > 0
                ? new StatisticRecord(Counter, TimeSpan.FromSeconds(History.Average(r => r.Delay.TotalSeconds)), History.Max(r => r.Start), History.Average(r => r.DataCount))
                : new StatisticRecord(0, TimeSpan.FromSeconds(0), DateTime.MinValue, 0d);
        public async Task RegistredFinalAsync(string query, DateTime start, TimeSpan delay, int dataCount)
            => await Task.Run(() => History.Push(new ImplementationRecord(query, delay, start, dataCount)));
        public async Task RegistredFinalAsync(ImplementationRecord record)
        {
            await Task.Run(() => {
                Counter++;
                History.Push(record);
            });
        }
    }
}