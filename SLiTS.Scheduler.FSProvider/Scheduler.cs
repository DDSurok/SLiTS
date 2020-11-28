﻿using Newtonsoft.Json;
using SLiTS.Api;
using SLiTS.Api.Throw;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SLiTS.Scheduler.FSProvider
{
    public class Scheduler : AScheduler
    {
        public Scheduler(string pluginDirectory, string storeDirectory, string scheduleDirectory, string configDirectory)
            : base(pluginDirectory)
        {
            StoreDirectory = storeDirectory;
            ScheduleDirectory = scheduleDirectory;
            ConfigDirectory = configDirectory;
        }
        /// <summary>
        /// Каталог хранения быстрых задач
        /// </summary>
        public string StoreDirectory { get; }
        /// <summary>
        /// Каталог хранения планируемых задач
        /// </summary>
        public string ScheduleDirectory { get; }
        /// <summary>
        /// Каталог настроек обработчиков быстрых задач
        /// </summary>
        public string ConfigDirectory { get; }
        protected override async Task<IDictionary<string, string>> GetSharedPropertiesAsync()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            FileInfo fi = new FileInfo(Path.Combine(PluginDirectory, "properties.json"));
            if (fi.Exists)
            {
                using TextReader tr = fi.OpenText();
                string name = null;
                string buffer;
                while ((buffer = await tr.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(buffer))
                        continue;
                    if (name is null)
                    {
                        name = buffer.Trim();
                        continue;
                    }
                    result.Add(name, buffer.Trim());
                }
            }
            return result;
        }
        protected override IEnumerable<FastTaskConfig> FastTaskConfigsIterator()
        {
            foreach(FileInfo fi in new DirectoryInfo(ConfigDirectory).EnumerateFiles("*.json"))
            {
                FastTaskConfig config;
                try
                {
                    using StreamReader reader = fi.OpenText();
                    config = JsonConvert.DeserializeObject<FastTaskConfig>(reader.ReadToEnd());
                }
                catch(Exception ex)
                {
                    if (Logger.IsWarnEnabled)
                        Logger.Warn(ex, $"Ошибка при чтении настроек быстрой задачи \"{fi.FullName}\"");
                    continue;
                }
                yield return config;
            }
        }
        protected override async IAsyncEnumerable<FastTaskRequest> FastTaskRequestIteratorAsync([EnumeratorCancellation] CancellationToken token)
        {
            DirectoryInfo storeDir = new DirectoryInfo(StoreDirectory);
            if (!storeDir.Exists)
                storeDir.Create();
            while (true)
            {
                await Task.Delay(1000);
                if (token.IsCancellationRequested)
                {
                    yield break;
                }
                foreach(FileInfo reqFile in storeDir.EnumerateFiles("*.json").OrderByDescending(fi => fi.LastWriteTime))
                {
                    FastTaskRequest request;
                    try
                    {
                        using StreamReader sr = reqFile.OpenText();
                        request = JsonConvert.DeserializeObject<FastTaskRequest>(await sr.ReadToEndAsync());
                        request.Id = reqFile.Name[0..^5];
                    }
                    catch
                    {
                        reqFile.MoveTo(reqFile.FullName[0..^4] + "bad");
                        continue;
                    }
                    reqFile.MoveTo(reqFile.FullName + ".ok");
                    yield return request;
                }
            }
        }
        protected override Task SaveFastTaskResponse(FastTaskResponse response)
            => File.WriteAllTextAsync(Path.Combine(StoreDirectory, response.Id + ".data"),
                                      JsonConvert.SerializeObject(response));
        protected override async IAsyncEnumerable<(Schedule schedule, bool isRunning)> GetAllSchedulesAsync()
        {
            DirectoryInfo scheduleDir = new DirectoryInfo(ScheduleDirectory);
            foreach (FileInfo fileInfo in scheduleDir.EnumerateFiles("*.json"))
            {
                using StreamReader file = fileInfo.OpenText();
                Schedule schedule = JsonConvert.DeserializeObject<Schedule>(await file.ReadToEndAsync());
                schedule.Id = fileInfo.Name[0..^5];
                string lockFile = fileInfo.FullName[0..^5] + ".lock";
                yield return (schedule, File.Exists(lockFile));
            }
        }
        protected override async Task StartScheduleTaskInStorageAsync(Schedule schedule)
        {
            string filePath = Path.Combine(ScheduleDirectory, $"{schedule.Id}.json");
            string lockPath = Path.Combine(ScheduleDirectory, $"{schedule.Id}.lock");
            if (!File.Exists(filePath))
            {
                throw new BaseScheduleException(schedule, "Не найдено задание");
            }
            Schedule scheduleInStorage = JsonConvert.DeserializeObject<Schedule>(await File.ReadAllTextAsync(filePath));
            scheduleInStorage.LastRunning = DateTime.Now;
            if (File.Exists(lockPath))
            {
                throw new BaseScheduleException(schedule, "Задание уже исполняется");
            }
            await File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(scheduleInStorage));
            await File.WriteAllTextAsync(lockPath, "");
        }
        protected override async Task FinishScheduleTaskInStorageAsync(Schedule schedule)
        {
            string filePath = Path.Combine(ScheduleDirectory, $"{schedule.Id}.json");
            string lockPath = Path.Combine(ScheduleDirectory, $"{schedule.Id}.lock");
            if (!File.Exists(filePath))
            {
                throw new BaseScheduleException(schedule, "Не найдено задание");
            }
            if (!File.Exists(filePath))
            {
                throw new BaseScheduleException(schedule, "Задание не исполняется");
            }
            Schedule scheduleInStorage = JsonConvert.DeserializeObject<Schedule>(await File.ReadAllTextAsync(filePath));
            scheduleInStorage.Parameters = schedule.Parameters;
            scheduleInStorage.Repeat = schedule.Repeat;
            scheduleInStorage.Active = schedule.Active;
            await File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(scheduleInStorage));
            File.Delete(lockPath);
        }
    }
}
