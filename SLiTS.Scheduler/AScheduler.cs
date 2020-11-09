using Autofac;
using Autofac.Extras.NLog;
using NLog;
using SLiTS.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
        ConcurrentDictionary<string, (ATask, Task)> ActiveTasks = new ConcurrentDictionary<string, (ATask, Task)>();
        public abstract Schedule ReadAsync();
        public abstract IEnumerable<(string handler, string title, string @params)> FastTaskParamsIterator();
        public abstract IAsyncEnumerable<(string taskTitle, FastTaskRequest request)> FastTaskRequestIterator();
        public abstract (string scheduleId, Schedule schedule) GetFirstScheduleTask();
        public abstract void StartScheduleTask(string scheduleId);
        public abstract void UpdateScheduleTask(string scheduleId, Schedule schedule);
        public abstract void EndScheduleTask(string scheduleId);
        public void Initialize()
        {
            foreach ((string handler, string title, string @params) in FastTaskParamsIterator())
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
            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
            Task fastTaskScheduler = new Task(StartFastTaskScheduler, cancelTokenSource.Token);
            Task taskScheduler = new Task(StartTaskScheduler, cancelTokenSource.Token);
            Task.WaitAll(fastTaskScheduler, taskScheduler);
        }
        private async void StartTaskScheduler()
        {
            Thread.Sleep(5000);
            while (true)
            {
                Thread.Sleep(1000);
                (string scheduleId, Schedule schedule) = GetFirstScheduleTask();
                if (string.IsNullOrWhiteSpace(scheduleId))
                {
                    Thread.Sleep(10000);
                    continue;
                }
                if (Logger.IsDebugEnabled)
                    Logger.Debug($@"Найдена задача, ожидающая выполнения - ScheduleID: ""{scheduleId}""");
                if (Logger.IsTraceEnabled)
                    Logger.Trace($"Schedule Info: {schedule}");
                if (TaskContainer.TryResolve(Type.GetType(schedule.TaskHandler), out object t) && t is ATask task)
                {
                    StartScheduleTask(scheduleId);
                    schedule.LastRunning = DateTime.Now;
                    UpdateScheduleTask(scheduleId, schedule);
                    task.Params = schedule.Parameters;
                    if (await task.Test())
                    {
                        ActiveTasks.TryAdd(scheduleId, (task, null));
                        ActiveTasks[scheduleId]
                            = (
                                ActiveTasks[scheduleId].Item1,
                                ActiveTasks[scheduleId].Item1.InvokeAsync(() => CompleteScheduleTask(scheduleId, schedule, task))
                            );
                        ActiveTasks[scheduleId].Item2.Start();
                    } else {
                        CompleteScheduleTask(scheduleId, schedule, task);
                    }
                } else {
                    if (Logger.IsWarnEnabled)
                        Logger.Warn($"Не найден класс, описывающий исполнение задачи ScheduleID: \"{scheduleId}\"");
                    if (Logger.IsDebugEnabled)
                        Logger.Debug($"Schedule Info: {schedule}");
                    schedule.Repeat = false;
                    CompleteScheduleTask(scheduleId, schedule, null);
                }
            }
        }
        private void CompleteScheduleTask(string scheduleId, Schedule schedule, ATask task)
        {
            if (!schedule.Repeat)
                schedule.Active = false;
            if (!(task is null))
                schedule.Parameters = task.Params;
            UpdateScheduleTask(scheduleId, schedule);
            EndScheduleTask(scheduleId);
            ActiveTasks.TryRemove(scheduleId, out _);
        }
        private void StartFastTaskScheduler()
        {
            throw new NotImplementedException();
        }
    }
}
