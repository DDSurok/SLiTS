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
using System.Runtime.CompilerServices;
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
        protected readonly Dictionary<string, AFastTask> FastTaskHandlers = new Dictionary<string, AFastTask>();
        protected IContainer TaskContainer { get; }
        protected IContainer FastTaskContainer { get; }
        protected ILogger Logger { get; }
        protected readonly ConcurrentDictionary<string, (ATask, Task)> ActiveTasks = new ConcurrentDictionary<string, (ATask, Task)>();
        protected abstract IEnumerable<(string handler, string title, string @params)> FastTaskParamsIterator();
        protected abstract IAsyncEnumerable<(string taskTitle, FastTaskRequest request)> FastTaskRequestIteratorAsync(CancellationToken token);
        protected abstract Task SaveFastTaskResponse(string taskId, Data response);
        protected abstract (string scheduleId, Schedule schedule) GetFirstScheduleTaskFromStorage();
        protected abstract void StartScheduleTaskInStorage(string scheduleId);
        protected abstract void UpdateScheduleTaskInStorage(string scheduleId, Schedule schedule);
        protected abstract void FinishScheduleTaskInStorage(string scheduleId);
        public void Initialize()
        {
            foreach ((string handler, string title, string @params) in FastTaskParamsIterator())
            {
                if (FastTaskContainer.TryResolve(Type.GetType(handler), out object task))
                {
                    AFastTask fastTask = (AFastTask)task;
                    fastTask.Params = @params;
                    FastTaskHandlers.Add(title, fastTask);
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
            CancellationToken token = cancelTokenSource.Token;
            Task fastTaskScheduler = new Task(async () => await StartFastTaskSchedulerAsync(token), token);
            Task taskScheduler = new Task(async () => await StartTaskScheduler(token), token);
            Task.WaitAll(fastTaskScheduler, taskScheduler);
        }
        private async Task StartTaskScheduler(CancellationToken token)
        {
            Thread.Sleep(5000);
            while (true)
            {
                Thread.Sleep(1000);
                (string scheduleId, Schedule schedule) = GetFirstScheduleTaskFromStorage();
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
                    StartScheduleTaskInStorage(scheduleId);
                    schedule.LastRunning = DateTime.Now;
                    UpdateScheduleTaskInStorage(scheduleId, schedule);
                    task.Params = schedule.Parameters;
                    if (await task.Test())
                    {
                        ActiveTasks.TryAdd(scheduleId, (task, null));
                        Task execTask = new Task(async () =>
                        {
                            await ActiveTasks[scheduleId].Item1.InvokeAsync(token);
                            FinishScheduleTask(scheduleId, schedule, task);
                        }, token);
                        ActiveTasks[scheduleId] = ( ActiveTasks[scheduleId].Item1, execTask );
                        ActiveTasks[scheduleId].Item2.Start();
                    } else {
                        FinishScheduleTask(scheduleId, schedule, task);
                    }
                } else {
                    if (Logger.IsWarnEnabled)
                        Logger.Warn($"Не найден класс, описывающий исполнение задачи ScheduleID: \"{scheduleId}\"");
                    if (Logger.IsDebugEnabled)
                        Logger.Debug($"Schedule Info: {schedule}");
                    schedule.Repeat = false;
                    FinishScheduleTask(scheduleId, schedule, null);
                }
            }
        }
        private async Task StartFastTaskSchedulerAsync(CancellationToken token)
        {
            await foreach((string taskTitle, FastTaskRequest request) in FastTaskRequestIteratorAsync(token))
            {
                if (FastTaskHandlers.ContainsKey(taskTitle))
                {
                    try
                    {
                        await SaveFastTaskResponse(request.Id, await FastTaskHandlers[taskTitle].InvokeAsync(request.Query));
                    }
                    catch (Exception ex)
                    {
                        if (Logger.IsErrorEnabled)
                            Logger.Error(ex, @$"При выполнении задачи ""{taskTitle}"" произошла ошибка");
                    }
                }
                else
                {
                    if (Logger.IsWarnEnabled)
                        Logger.Warn(@$"Для задач ""{taskTitle}"" отсутствует обработчик");
                }
            }
        }
        private void FinishScheduleTask(string scheduleId, Schedule schedule, ATask task)
        {
            if (!schedule.Repeat)
                schedule.Active = false;
            if (!(task is null))
                schedule.Parameters = task.Params;
            UpdateScheduleTaskInStorage(scheduleId, schedule);
            FinishScheduleTaskInStorage(scheduleId);
            ActiveTasks.TryRemove(scheduleId, out _);
        }
    }
}
