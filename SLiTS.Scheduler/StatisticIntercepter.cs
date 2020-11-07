using SLiTS.Api;
using SLiTS.Standard;
using System;
using System.Threading.Tasks;

namespace SLiTS.Scheduler
{
    public class StatisticIntercepter<T> : IAsyncStatisticIntercepter<T> where T : AFastTask
    {
        public int Counter { get; } = 0;
        public int Limit { get; }
        public StatisticIntercepter(int limit)
        {
            Limit = limit;
            History = new LimitedQueue<ImplementationRecord>(Limit);
        }
        public LimitedQueue<ImplementationRecord> History { get; }
        public async Task RegistredFinalAsync(string query, DateTime start, TimeSpan delay, int dataCount)
        {
            await Task.Run(() => History.Push(new ImplementationRecord(query, delay, start, dataCount)));
        }

        public async Task RegistredFinalAsync(ImplementationRecord record)
        {
            await Task.Run(() => History.Push(record));
        }
    }
}