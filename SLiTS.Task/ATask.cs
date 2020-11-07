using NLog;
using System.Threading.Tasks;

namespace SLiTS.Api
{
    public abstract class ATask
    {
        public ATask(string @params, string title, ILogger logger)
        {
            Params = @params;
            Title = title;
            Logger = logger;
        }
        public string Params { get; }
        public string Title { get; }
        public ILogger Logger { get; }
        public abstract Task Invoke(string @params);
        public abstract Task<bool> Test(string @params);
    }
}
