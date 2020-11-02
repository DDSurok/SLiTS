using System;

namespace SLiTS.Api
{
    public class ImplementationRecord
    {
        public string Query { get; set; }
        public TimeSpan Delay { get; set; }
        public DateTime Start { get; set; }
        public int DataCount { get; set; }
    }
}