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
            PluginDirectory = pluginDirectory;
            ContainerBuilder taskBuilder = new ContainerBuilder();
            taskBuilder.RegisterModule<NLogModule>();
            ContainerBuilder fastTaskBuilder = new ContainerBuilder();
            fastTaskBuilder.RegisterModule<NLogModule>();
            SharedPropertyProvider propertyProvider = new SharedPropertyProvider(GetSharedPropertiesAsync().Result);
            fastTaskBuilder.RegisterGeneric(typeof(StatisticIntercepter<>)).AsSelf().As(typeof(IAsyncStatisticIntercepter<>));
            taskBuilder.RegisterInstance(propertyProvider).AsSelf().As<ISharedPropertyProvider>();
            fastTaskBuilder.RegisterInstance(propertyProvider).AsSelf().As<ISharedPropertyProvider>();
            foreach (FileInfo fi in new DirectoryInfo(pluginDirectory).EnumerateFiles("*.dll"))
            {
                Assembly assembly = Assembly.LoadFrom(fi.FullName);
                foreach (Type type in assembly.GetTypes()
                                              .Where(t => !t.IsAbstract)
                                              .Where(t => TestClassImplements(t, typeof(ALongTermTask))))
                {
                    taskBuilder.RegisterType(type).AsSelf();
                }
                foreach (Type type in assembly.GetTypes()
                                              .Where(t => !t.IsAbstract)
                                              .Where(t => TestClassImplements(t, typeof(AQuickTask))))
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
        protected readonly Dictionary<string, AQuickTask> FastTaskHandlers = new Dictionary<string, AQuickTask>();
        protected IContainer TaskContainer { get; }
        protected IContainer FastTaskContainer { get; }
        protected ILogger Logger { get; }
        protected string PluginDirectory { get; }
        protected ConcurrentDictionary<string, (ALongTermTask, Task)> ActiveLongTermTasks { get; }
            = new ConcurrentDictionary<string, (ALongTermTask, Task)>();
        protected abstract IEnumerable<QuickTaskConfig> QuickTaskConfigsIterator();
        protected abstract Task<IDictionary<string, string>> GetSharedPropertiesAsync();
        protected abstract IAsyncEnumerable<QuickTaskRequest> QuickTaskRequestIteratorAsync(CancellationToken token);
        protected abstract Task SaveQuickTaskResponse(QuickTaskResponse response);
        protected abstract IAsyncEnumerable<(LongTermTaskSchedule schedule, bool isRunning)> GetAllLongTermTaskSchedulesAsync();
        private async Task<LongTermTaskSchedule> GetFirstLongTermTaskScheduleFromStorageAsync()
        {
            IEnumerable<string> usingResources = new string[0];
            List<LongTermTaskSchedule> tasks = new List<LongTermTaskSchedule>();
            await foreach ((LongTermTaskSchedule schedule, bool isRunning) in GetAllLongTermTaskSchedulesAsync())
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
        protected abstract Task StartLongTermTaskScheduleInStorageAsync(LongTermTaskSchedule schedule);
        protected abstract Task FinishLongTermTaskScheduleInStorageAsync(LongTermTaskSchedule schedule);
        public void Initialize()
        {
            foreach (QuickTaskConfig config in QuickTaskConfigsIterator())
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == config.Handler);
                if (FastTaskContainer.TryResolve(type, out object task))
                {
                    AQuickTask fastTask = (AQuickTask)task;
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
            Task taskScheduler = Task.Run(async () => await StartLongTermTaskScheduler(token), token);
            await Task.WhenAll(new[] { fastTaskScheduler, taskScheduler });
        }
        private async Task StartLongTermTaskScheduler(CancellationToken token)
        {
            for (int i = 5; i > 0; i--)
            {
                if (Logger.IsTraceEnabled)
                    Logger.Trace($"Планировщик запустится через {i} секунд...");
                await Task.Delay(1000);
            }
            while (true)
            {
                try
                {
                    await Task.Delay(100);
                    LongTermTaskSchedule schedule = await GetFirstLongTermTaskScheduleFromStorageAsync();
                    if (schedule is null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    if (Logger.IsDebugEnabled)
                        Logger.Debug($@"Найдена задача, ожидающая выполнения - ScheduleID: ""{schedule.Id}""");
                    if (Logger.IsTraceEnabled)
                        Logger.Trace($"Schedule Info: {schedule}");
                    Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == schedule.ClassHandler);
                    if (TaskContainer.TryResolve(type, out object t) && t is ALongTermTask task)
                    {
                        await StartLongTermTaskScheduleInStorageAsync(schedule);
                        task.Params = schedule.Parameters;
                        if (await task.TestAsync())
                        {
                            ActiveLongTermTasks.TryAdd(schedule.Id, (task, null));
                            Task execTask = new Task(async () =>
                            {
                                await ActiveLongTermTasks[schedule.Id].Item1.InvokeAsync(token);
                                await FinishingLongTermTaskAsync(schedule, task);
                            }, token);
                            ActiveLongTermTasks[schedule.Id] = (ActiveLongTermTasks[schedule.Id].Item1, execTask);
                            ActiveLongTermTasks[schedule.Id].Item2.Start();
                        } else
                            await FinishingLongTermTaskAsync(schedule, task);
                    }
                    else
                    {
                        if (Logger.IsWarnEnabled)
                            Logger.Warn($"Не найден класс, описывающий исполнение задачи ScheduleID: \"{schedule.Id}\"");
                        if (Logger.IsDebugEnabled)
                            Logger.Debug($"Schedule Info: {schedule}");
                        schedule.Repeat = false;
                        await FinishingLongTermTaskAsync(schedule, null);
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
            await foreach(QuickTaskRequest request in QuickTaskRequestIteratorAsync(token))
            {
                if (FastTaskHandlers.ContainsKey(request.Title))
                {
                    Task task = new Task(async () =>
                        {
                            try
                            {
                                await SaveQuickTaskResponse(await FastTaskHandlers[request.Title].InvokeAsync(request));
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
        private async Task FinishingLongTermTaskAsync(LongTermTaskSchedule schedule, ALongTermTask task)
        {
            if (!schedule.Repeat)
                schedule.Active = false;
            if (!(task is null))
                schedule.Parameters = task.Params;
            await FinishLongTermTaskScheduleInStorageAsync(schedule);
            ActiveLongTermTasks.TryRemove(schedule.Id, out _);
        }
    }
}
