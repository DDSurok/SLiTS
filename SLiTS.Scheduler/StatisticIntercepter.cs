﻿using SLiTS.Api;
using System;
using System.Threading.Tasks;

namespace SLiTS.Scheduler
{
    public class StatisticIntercepter<T> : IAsyncStatisticIntercepter<T> where T : AFastTask
    {
        public int Counter { get; } = 0;
        public int Limit { get; }
        public StatisticIntercepter()
        {
            Limit = 10;
            History = new LimitedConcurrentQueue<ImplementationRecord>(Limit);
        }
        public LimitedConcurrentQueue<ImplementationRecord> History { get; }
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