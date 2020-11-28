using System.Collections.Generic;

namespace SLiTS.Api
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
}