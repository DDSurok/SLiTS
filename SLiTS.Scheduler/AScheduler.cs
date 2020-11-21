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
        protected AScheduler(string pluginDirectory)
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
            foreach (FileInfo fi in new DirectoryInfo(pluginDirectory).GetFiles("*.dll"))
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
        protected abstract IEnumerable<FastTaskConfig> FastTaskConfigsIterator();
        protected abstract IAsyncEnumerable<FastTaskRequest> FastTaskRequestIteratorAsync(CancellationToken token);
        protected abstract Task SaveFastTaskResponse(FastTaskResponse response);
        protected abstract IAsyncEnumerable<(string scheduleId, Schedule schedule, bool isRunning)> GetAllSchedulesAsync();
        private async Task<(string scheduleId, Schedule schedule)> GetFirstScheduleTaskFromStorageAsync()
        {
            IEnumerable<string> usingResources = new string[] { };
            List<(string scheduleId, Schedule schedule, bool isRunning)> tasks = new List<(string scheduleId, Schedule schedule, bool isRunning)>();
            await foreach (var item in GetAllSchedulesAsync())
            {
                if (item.isRunning)
                {
                    usingResources = usingResources.Concat(item.schedule.UsingResource).Distinct();
                }
                if (item.schedule.Active && item.schedule.TestInQueue())
                {
                    tasks.Add(item);
                }
            }
            return tasks.Where(r => r.schedule.UsingResource.Intersect(usingResources).Any())
                        .OrderByDescending(r => r.schedule.GetRealWaiting())
                        .Select(r => (r.scheduleId, r.schedule))
                        .FirstOrDefault();
        }
        protected abstract Task StartScheduleTaskInStorageAsync(string scheduleId);
        protected abstract Task UpdateScheduleTaskInStorageAsync(Schedule schedule);
        protected abstract Task FinishScheduleTaskInStorageAsync(string scheduleId);
        public void Initialize()
        {
            foreach (FastTaskConfig config in FastTaskConfigsIterator())
            {
                if (FastTaskContainer.TryResolve(Type.GetType(config.Handler), out object task))
                {
                    AFastTask fastTask = (AFastTask)task;
                    fastTask.Params = config.Parameters;
                    FastTaskHandlers.Add(config.Title, fastTask);
                }
                else
                {
                    if (Logger.IsInfoEnabled)
                    {
                        Logger.Info($@"Не удалось зарегистрировать обработчик {config.Handler} для задачи {config.Title}.");
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
                (string scheduleId, Schedule schedule) = await GetFirstScheduleTaskFromStorageAsync();
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
                    await StartScheduleTaskInStorageAsync(scheduleId);
                    schedule.LastRunning = DateTime.Now;
                    await UpdateScheduleTaskInStorageAsync(schedule);
                    task.Params = schedule.Parameters;
                    if (await task.Test())
                    {
                        ActiveTasks.TryAdd(schedule.Id, (task, null));
                        Task execTask = new Task(async () =>
                        {
                            await ActiveTasks[scheduleId].Item1.InvokeAsync(token);
                            await FinishScheduleTaskAsync(schedule, task);
                        }, token);
                        ActiveTasks[scheduleId] = ( ActiveTasks[scheduleId].Item1, execTask );
                        ActiveTasks[scheduleId].Item2.Start();
                    } else {
                        await FinishScheduleTaskAsync(schedule, task);
                    }
                } else {
                    if (Logger.IsWarnEnabled)
                        Logger.Warn($"Не найден класс, описывающий исполнение задачи ScheduleID: \"{scheduleId}\"");
                    if (Logger.IsDebugEnabled)
                        Logger.Debug($"Schedule Info: {schedule}");
                    schedule.Repeat = false;
                    await FinishScheduleTaskAsync(schedule, null);
                }
            }
        }
        private async Task StartFastTaskSchedulerAsync(CancellationToken token)
        {
            await foreach(FastTaskRequest request in FastTaskRequestIteratorAsync(token))
            {
                if (FastTaskHandlers.ContainsKey(request.Title))
                {
                    try
                    {
                        await SaveFastTaskResponse(await FastTaskHandlers[request.Title].InvokeAsync(request));
                    }
                    catch (Exception ex)
                    {
                        if (Logger.IsErrorEnabled)
                            Logger.Error(ex, @$"При выполнении задачи ""{request.Title}"" произошла ошибка");
                    }
                }
                else
                {
                    if (Logger.IsWarnEnabled)
                        Logger.Warn(@$"Для задач ""{request.Title}"" отсутствует обработчик");
                }
            }
        }
        private async Task FinishScheduleTaskAsync (Schedule schedule, ATask task)
        {
            if (!schedule.Repeat)
                schedule.Active = false;
            if (!(task is null))
                schedule.Parameters = task.Params;
            await UpdateScheduleTaskInStorageAsync(schedule);
            await FinishScheduleTaskInStorageAsync(schedule.Id);
            ActiveTasks.TryRemove(schedule.Id, out _);
        }
    }
}
