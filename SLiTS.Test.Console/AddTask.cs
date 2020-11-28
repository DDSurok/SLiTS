using NLog;
using SLiTS.Api;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SLiTS.Test.Console
{
    public class AddTask : ATask
    {
        private int shiftVal;
        public AddTask(ILogger logger, ISharedPropertyProvider propertyProvider) : base(logger, propertyProvider)
        {
            shiftVal = int.Parse(propertyProvider["shiftCount"]);
        }
        public override async Task InvokeAsync(CancellationToken token)
        {
            int val = int.Parse(Params);
            val *= shiftVal;
            Params = val.ToString();
            await Task.Delay(new Random().Next(10000, 60000));
        }

        public override async Task<bool> TestAsync()
        {
            return true;
        }
    }
}