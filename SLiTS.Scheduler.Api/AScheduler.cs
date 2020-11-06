using Autofac;
using Microsoft.Extensions.Logging;
using SLiTS.Api;
using System;
using System.Collections.Generic;

namespace SLiTS.Scheduler.Api
{
    public abstract class AScheduler
    {
        protected AScheduler(string workingDirectory)
        {
            Builder = new ContainerBuilder
        }
        protected Dictionary<string, AFastTask> _fastTaskHandlers = new Dictionary<string, AFastTask>();
        protected ContainerBuilder Builder { get; }
        protected ILogger Logger { get; }
        public abstract Schedule ReadAsync();
        public abstract IEnumerable<(string handler, string title, string @params)> ReadFastTaskParams();

        public void Initialize()
        {
            foreach((string handler, string title, string @params) in ReadFastTaskParams())
            {
                try
                {
                    Builder.
                }
                catch(Exception ex)
                {
                    Logger.LogInformation($@"Во время регистрации обработчика для задачи ", ex);
                }
            }
        }
    }
}
