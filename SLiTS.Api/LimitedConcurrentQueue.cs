using System.Collections.Concurrent;

namespace SLiTS.Api
{
    public class LimitedConcurrentQueue<T> : ConcurrentQueue<T>
    {
        public LimitedConcurrentQueue(int limit)
        {
            Limit = limit;
        }

        public int Limit { get; }

        public void Push(T item)
        {
            lock (this)
            {
                if (Count == Limit)
                {
                    TryDequeue(out _);
                }
                Enqueue(item);
            }
        }
    }
}