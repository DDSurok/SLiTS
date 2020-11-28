using SLiTS.Api;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SLiTS.Scheduler
{
    public class SharedPropertyProvider : ISharedPropertyProvider
    {
        private ConcurrentDictionary<string, string> _dictionary;
        public SharedPropertyProvider(IDictionary<string, string> dictionary)
        {
            _dictionary = new ConcurrentDictionary<string, string>(dictionary);
        }
        public string this[string name] => _dictionary[name];
    }
}
