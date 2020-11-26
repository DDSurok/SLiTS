using Autofac;
using Autofac.Extras.NLog;
using NLog;
using SLiTS.Api;
using SLiTS.Api.Throw;
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
            static bool TestClassImplements(Type type, Type testType)
            {
                if (type is null || type == typeof(object))
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
            fastTaskBuilder.RegisterGeneric(typeof(StatisticIntercepter<>)).AsSelf().As(typeof(IAsyncStatisticIntercepter<>));
            foreach (FileInfo fi in new DirectoryInfo(pluginDirectory).EnumerateFiles("*.dll"))
            {
                Assembly assembly = Assembly.LoadFrom(fi.FullName);
                foreach (Type type in assembly.GetTypes()
                                              .Where(t => !t.IsAbstract)
                                              .Where(t => TestClassImplements(t, typeof(ATask))))
                {
                    taskBuilder.RegisterType(type).AsSelf();
                }
                foreach (Type type in assembly.GetTypes()
                                              .Where(t => !t.IsAbstract)
                                              .Where(t => TestClassImplements(t, typeof(AFastTask))))
                {
                    fastTaskBuilder.RegisterType(type).AsSelf();
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
        protected abstract IAsyncEnumerable<(Schedule schedule, bool isRunning)> GetAllSchedulesAsync();
        private async Task<Schedule> GetFirstScheduleTaskFromStorageAsync()
        {
            IEnumerable<string> usingResources = new string[0];
            List<Schedule> tasks = new List<Schedule>();
            await foreach ((Schedule schedule, bool isRunning) in GetAllSchedulesAsync())
            {
                if (isRunning)
                {
                    usingResources = usingResources.Concat(schedule.UsingResource).Distinct();
                    continue;
                }
                if (schedule.Active && schedule.TestInQueue())
                {
                    tasks.Add(schedule);
                }
            }
            return tasks.Where(r => !r.UsingResource.Intersect(usingResources).Any())
                        .OrderByDescending(r => r.GetRealWaiting())
                        .FirstOrDefault();
        }
        protected abstract Task StartScheduleTaskInStorageAsync(Schedule schedule);
        protected abstract Task FinishScheduleTaskInStorageAsync(Schedule schedule);
        public void Initialize()
        {
            foreach (FastTaskConfig config in FastTaskConfigsIterator())
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == config.Handler);
                if (FastTaskContainer.TryResolve(type, out object task))
                {
                    AFastTask fastTask = (AFastTask)task;
                    fastTask.Parameters = config.Parameters;
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
        public async Task StartAsync()
        {
            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
            CancellationToken token = cancelTokenSource.Token;
            Task fastTaskScheduler = Task.Run(async () => await StartFastTaskSchedulerAsync(token), token);
            Task taskScheduler = Task.Run(async () => await StartTaskScheduler(token), token);
            await Task.WhenAll(new[] { fastTaskScheduler, taskScheduler });
        }
        private async Task StartTaskScheduler(CancellationToken token)
        {
            await Task.Delay(500);
            while (true)
            {
                try
                {
                    await Task.Delay(100);
                    Schedule schedule = await GetFirstScheduleTaskFromStorageAsync();
                    if (schedule is null)
                    {
                        Thread.Sleep(10000);
                        continue;
                    }
                    if (Logger.IsDebugEnabled)
                        Logger.Debug($@"Найдена задача, ожидающая выполнения - ScheduleID: ""{schedule.Id}""");
                    if (Logger.IsTraceEnabled)
                        Logger.Trace($"Schedule Info: {schedule}");
                    Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == schedule.TaskHandler);
                    if (TaskContainer.TryResolve(type, out object t) && t is ATask task)
                    {
                        await StartScheduleTaskInStorageAsync(schedule);
                        task.Params = schedule.Parameters;
                        if (await task.TestAsync())
                        {
                            ActiveTasks.TryAdd(schedule.Id, (task, null));
                            Task execTask = new Task(async () =>
                            {
                                await ActiveTasks[schedule.Id].Item1.InvokeAsync(token);
                                await FinishScheduleTaskAsync(schedule, task);
                            }, token);
                            ActiveTasks[schedule.Id] = (ActiveTasks[schedule.Id].Item1, execTask);
                            ActiveTasks[schedule.Id].Item2.Start();
                        } else
                            await FinishScheduleTaskAsync(schedule, task);
                    }
                    else
                    {
                        if (Logger.IsWarnEnabled)
                            Logger.Warn($"Не найден класс, описывающий исполнение задачи ScheduleID: \"{schedule.Id}\"");
                        if (Logger.IsDebugEnabled)
                            Logger.Debug($"Schedule Info: {schedule}");
                        schedule.Repeat = false;
                        await FinishScheduleTaskAsync(schedule, null);
                    }
                }
                catch (BaseScheduleException ex)
                {
                    if (Logger.IsWarnEnabled)
                        Logger.Warn(ex);
                }
                catch (Exception ex)
                {
                    if (Logger.IsFatalEnabled)
                        Logger.Fatal(ex, "В процессе подготовки и выполнения задачи по расписанию произошла непредвиденная ошибка. Ошибка требует обязательного внимания разработчика и администратора");
                }
            }
        }
        private async Task StartFastTaskSchedulerAsync(CancellationToken token)
        {
            await foreach(FastTaskRequest request in FastTaskRequestIteratorAsync(token))
            {
                if (FastTaskHandlers.ContainsKey(request.Title))
                {
                    Task task = new Task(async () =>
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
                    });
                    task.Start();
                }
                else
                {
                    if (Logger.IsWarnEnabled)
                        Logger.Warn(@$"Для задач ""{request.Title}"" отсутствует обработчик");
                }
            }
            if (Logger.IsTraceEnabled)
                Logger.Trace("Выполнение быстрых задач прекращено");
        }
        private async Task FinishScheduleTaskAsync (Schedule schedule, ATask task)
        {
            if (!schedule.Repeat)
                schedule.Active = false;
            if (!(task is null))
                schedule.Parameters = task.Params;
            await FinishScheduleTaskInStorageAsync(schedule);
            ActiveTasks.TryRemove(schedule.Id, out _);
        }
    }
}
