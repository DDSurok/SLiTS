using Autofac;
using Microsoft.Extensions.Logging;
using SLiTS.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SLiTS.Scheduler.Api
{
    public abstract class AScheduler
    {
        protected AScheduler(string workingDirectory)
        {
            bool TestClassImplements(Type type, Type testType)
            {
                if (type == typeof(object))
                {
                    return false;
                }
                if (type == testType)
                {
                    return true;
                }
                return TestClassImplements(type.BaseType, testType);
            }

            ContainerBuilder taskBuilder = new ContainerBuilder();
            ContainerBuilder fastTaskBuilder = new ContainerBuilder();
            foreach (FileInfo fi in new DirectoryInfo(workingDirectory).GetFiles("*.dll"))
            {
                foreach(Type type in Assembly.LoadFrom(fi.FullName)
                                             .GetTypes()
                                             .Where(t => TestClassImplements(t, typeof(ATask))))
                {
                    taskBuilder.RegisterType(type);
                }
                foreach (Type type in Assembly.LoadFrom(fi.FullName)
                                             .GetTypes()
                                             .Where(t => TestClassImplements(t, typeof(AFastTask))))
                {
                    fastTaskBuilder.RegisterType(type);
                }
            }
            TaskContainer = taskBuilder.Build();
            FastTaskContainer = fastTaskBuilder.Build();
        }
        ~AScheduler()
        {
            TaskContainer.DisposeAsync().AsTask().Wait();
            FastTaskContainer.DisposeAsync().AsTask().Wait();
        }
        protected Dictionary<string, AFastTask> _fastTaskHandlers = new Dictionary<string, AFastTask>();
        protected IContainer TaskContainer { get; }
        protected IContainer FastTaskContainer { get; }
        protected ILogger Logger { get; }
        public abstract Schedule ReadAsync();
        public abstract IEnumerable<(string handler, string title, string @params)> ReadFastTaskParams();
        public void Initialize()
        {
            foreach((string handler, string title, string @params) in ReadFastTaskParams())
            {
                if (FastTaskContainer.TryResolve(Type.GetType(handler), out object task))
                {
                    AFastTask fastTask = (AFastTask)task;
                    fastTask.Params = @params;
                    _fastTaskHandlers.Add(title, fastTask);
                }
                else
                {
                    Logger.LogInformation($@"Не удалось зарегистрировать обработчик {handler} для задачи {title}.");
                }
            }
        }
    }
}
