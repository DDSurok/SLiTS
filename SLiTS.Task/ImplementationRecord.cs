using System;

namespace SLiTS.Api
{
    public class ImplementationRecord
    {
        public string Query { get; set; }
        public TimeSpan Delay { get; set; }
        public DateTime Start { get; set; }
        public int DataCount { get; set; }
        public void Deconstruct(out string query, out TimeSpan delay, out DateTime start, out int dataCount)
        {
            query = Query;
            delay = Delay;
            start = Start;
            dataCount = DataCount;
        }
        public ImplementationRecord() { }
        public ImplementationRecord(string query, TimeSpan delay, DateTime start, int dataCount)
            => (Query, Delay, Start, DataCount) = (query, delay, start, dataCount);
    }
}