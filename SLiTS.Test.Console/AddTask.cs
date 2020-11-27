using NLog;
using SLiTS.Api;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SLiTS.Test.Console
{
    public class AddTask : ATask
    {
        public AddTask(ILogger logger) : base(logger)
        {
        }
        public override async Task InvokeAsync(CancellationToken token)
        {
            int val = int.Parse(Params);
            val *= 2;
            Params = val.ToString();
            await Task.Delay(new Random().Next(10000, 60000));
        }

        public override async Task<bool> TestAsync()
        {
            return true;
        }
    }
}