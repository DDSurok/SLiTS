using Newtonsoft.Json;
using SLiTS.Api;
using SLiTS.Api.Throw;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        protected override IEnumerable<(string handler, string title, string @params)> FastTaskParamsIterator()
        {
            // TODO: Need implemented after create part of Fast Task Handling
            yield break;
        }
        protected override async IAsyncEnumerable<(string taskTitle, FastTaskRequest request)> FastTaskRequestIteratorAsync([EnumeratorCancellation] CancellationToken token)
        {
            while (true)
            {
                // TODO: Need implemented after create part of Fast Task Handling
                await Task.Delay(1000);
                if (token.IsCancellationRequested)
                {
                    yield break;
                }
            }
        }
        protected override Task SaveFastTaskResponse(string taskId, Data response)
        {
            throw new NotImplementedException();
        }
        protected override async IAsyncEnumerable<(string scheduleId, Schedule schedule, bool isRunning)> GetAllSchedulesAsync()
        {
            foreach (FileInfo fileInfo in new DirectoryInfo(ScheduleDirectory).GetFiles("*.json"))
            {
                using StreamReader file = fileInfo.OpenText();
                Schedule schedule = JsonConvert.DeserializeObject<Schedule>(await file.ReadToEndAsync());
                string lockFile = fileInfo.FullName[0..^5]; 
                yield return (fileInfo.Name, schedule, File.Exists(lockFile));
            }
        }
        protected override async Task StartScheduleTaskInStorageAsync(string scheduleId)
        {
            string filePath = Path.Combine(ScheduleDirectory, $"{scheduleId}.json");
            string lockPath = Path.Combine(ScheduleDirectory, $"{scheduleId}.lock");
            if (!File.Exists(filePath))
            {
                throw new BaseScheduleException(scheduleId, "Не найдено задание");
            }
            Schedule schedule = JsonConvert.DeserializeObject<Schedule>(await File.ReadAllTextAsync(filePath));
            schedule.LastRunning = DateTime.Now;
            if (File.Exists(filePath))
            {
                throw new BaseScheduleException(scheduleId, "Задание уже исполняется");
            }
            await File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(schedule));
            await File.WriteAllTextAsync(lockPath, "");
        }
        protected override async Task UpdateScheduleTaskInStorageAsync(string scheduleId, Schedule schedule)
        {
            string filePath = Path.Combine(ScheduleDirectory, $"{scheduleId}.json");
            if (!File.Exists(filePath))
            {
                throw new BaseScheduleException(scheduleId, "Не найдено задание");
            }
            await File.WriteAllTextAsync(filePath, JsonConvert.SerializeObject(schedule));
        }
        protected override Task FinishScheduleTaskInStorageAsync(string scheduleId)
        {
            string filePath = Path.Combine(ScheduleDirectory, $"{scheduleId}.json");
            string lockPath = Path.Combine(ScheduleDirectory, $"{scheduleId}.lock");
            if (!File.Exists(filePath))
            {
                throw new BaseScheduleException(scheduleId, "Не найдено задание");
            }
            if (!File.Exists(filePath))
            {
                throw new BaseScheduleException(scheduleId, "Задание не исполняется");
            }
            return Task.Run(() => File.Delete(lockPath));
        }
    }
}
