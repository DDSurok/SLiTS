﻿using Newtonsoft.Json;
using SLiTS.Api;
using System.IO;
using System.Linq;

namespace SLiTS.Test.Console
{
    class Program
    {
        static void Main(string[] args)
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
            scheduler.Start();

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
            foreach(int i in Enumerable.Range(2, 10))
            {
                FastTaskConfig config = new FastTaskConfig
                    {
                        Title = $"Pow ^{i}",
                        Handler = "SLiTS.Test.Console.PowFastTask",
                        Parameters = $"{i}"
                    };
                File.WriteAllText(Path.Combine(configDir.FullName, $"pow{i}.json"),
                                  JsonConvert.SerializeObject(config));
            }
        }
    }
}
