using Autofac;
using Autofac.Extras.NLog;
using NLog;
using SLiTS.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SLiTS.Scheduler
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

            Logger = LogManager.GetCurrentClassLogger();
            ContainerBuilder taskBuilder = new ContainerBuilder();
            taskBuilder.RegisterModule<NLogModule>();
            ContainerBuilder fastTaskBuilder = new ContainerBuilder();
            fastTaskBuilder.RegisterModule<NLogModule>();
            foreach (FileInfo fi in new DirectoryInfo(workingDirectory).GetFiles("*.dll"))
            {
                Assembly assembly = Assembly.LoadFrom(fi.FullName);
                foreach (Type type in assembly.GetTypes()
                                              .Where(t => TestClassImplements(t, typeof(ATask))))
                {
                    taskBuilder.RegisterType(type);
                }
                foreach (Type type in assembly.GetTypes()
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
            foreach ((string handler, string title, string @params) in ReadFastTaskParams())
            {
                if (FastTaskContainer.TryResolve(Type.GetType(handler), out object task))
                {
                    AFastTask fastTask = (AFastTask)task;
                    fastTask.Params = @params;
                    _fastTaskHandlers.Add(title, fastTask);
                }
                else
                {
                    if (Logger.IsInfoEnabled)
                    {
                        Logger.Info($@"Не удалось зарегистрировать обработчик {handler} для задачи {title}.");
                    }
                }
            }
        }
        public void Start()
        {
            Task fastTaskScheduler = new Task(StartFastTaskScheduler);
            Task taskScheduler = new Task(StartTaskScheduler);
            Task.WaitAll(fastTaskScheduler, taskScheduler);
        }

        private void StartTaskScheduler()
        {
            throw new NotImplementedException();
        }

        private void StartFastTaskScheduler()
        {
            throw new NotImplementedException();
        }
    }
}
