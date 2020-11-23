using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SLiTS.Standard
{
    public class LimitedQueue<T> : Queue<T>
    {
        public LimitedQueue(int limit)
        {
            Limit = limit;
        }

        public int Limit { get; }

        public void Push(T item)
        {
            if (Count == Limit)
            {
                Dequeue();
            }
            Enqueue(item);
        }
    }

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
