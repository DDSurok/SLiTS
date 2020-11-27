using Newtonsoft.Json;
using SLiTS.Api;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SLiTS.Test.Console
{
    class Program
    {
        static void Main()
        {
            DirectoryInfo rootDir = new DirectoryInfo(".");
            DirectoryInfo storeDir = rootDir.CreateSubdirectory("store");
            DirectoryInfo scheduleDir = rootDir.CreateSubdirectory("schedule");
            DirectoryInfo configDir = rootDir.CreateSubdirectory("config");
            InitDirectories(storeDir, scheduleDir, configDir);
            Scheduler.FSProvider.Scheduler scheduler
                = new Scheduler.FSProvider.Scheduler(pluginDirectory: rootDir.FullName,
                                                     storeDirectory: storeDir.FullName,
                                                     scheduleDirectory: scheduleDir.FullName,
                                                     configDirectory: configDir.FullName);
            scheduler.Initialize();
            scheduler.StartAsync().Wait();
        }

        private static void InitDirectories(DirectoryInfo storeDir, DirectoryInfo scheduleDir, DirectoryInfo configDir)
        {
            if (storeDir.Exists)
                storeDir.Delete(true);
            storeDir.Create();
            if (scheduleDir.Exists)
                scheduleDir.Delete(true);
            scheduleDir.Create();
            if (configDir.Exists)
                configDir.Delete(true);
            configDir.Create();
            foreach (int i in Enumerable.Range(2, 9))
            {
                FastTaskConfig config = new FastTaskConfig
                {
                    Title = $"Pow ^{i}",
                    Handler = typeof(PowFastTask).FullName,
                    Parameters = $"{i}"
                };
                File.WriteAllText(Path.Combine(configDir.FullName, $"pow{i}.json"),
                                  JsonConvert.SerializeObject(config));
            }
            //foreach (int i in Enumerable.Range(20, 80))
            //{
            //    foreach (int j in Enumerable.Range(2, 9))
            //    {
            //        FastTaskRequest request = new FastTaskRequest
            //        {
            //            Title = $"Pow ^{j}",
            //            Query = i.ToString()
            //        };
            //        File.WriteAllText(Path.Combine(storeDir.FullName, $"{System.Guid.NewGuid()}.json"),
            //                          JsonConvert.SerializeObject(request));
            //    }
            //}
            Schedule schedule = new Schedule
            {
                Active = true,
                BeginDailyPlan = new TimeSpan(0, 0, 0),
                EndDailyPlan = new TimeSpan(23, 59, 59),
                Id = Guid.NewGuid().ToString(),
                LastRunning = DateTime.MinValue,
                MinimalElapsed = new TimeSpan(0, 1, 0),
                Parameters = "1",
                Repeat = true,
                TaskHandler = typeof(AddTask).FullName,
                Title = "Сдвиг влево",
                UsingResource = new string[] { "1" },
                WeeklyPlan = new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday,
                    DayOfWeek.Saturday,
                    DayOfWeek.Sunday
                }
            };
            File.WriteAllText(Path.Combine(scheduleDir.FullName, $"{schedule.Id}.json"), JsonConvert.SerializeObject(schedule));
            schedule.Id = Guid.NewGuid().ToString();
            File.WriteAllText(Path.Combine(scheduleDir.FullName, $"{schedule.Id}.json"), JsonConvert.SerializeObject(schedule));
        }
    }
}
