using System;

namespace SLiTS.Api
{
    public class StatisticRecord
    {
        public int Count { get; private set; }
        public TimeSpan AvgDelay { get; private set; }
        public DateTime LastRun { get; private set; }
        public double AvgDataCount { get; private set; }
        public StatisticRecord(int count, TimeSpan avgDelay, DateTime lastRun, double avgDataCount)
            => (Count, AvgDelay, LastRun, AvgDataCount) = (count, avgDelay, lastRun, avgDataCount);
    }
}