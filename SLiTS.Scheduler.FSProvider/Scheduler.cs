using SLiTS.Api;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SLiTS.Scheduler.FSProvider
{
    public class Scheduler : AScheduler
    {
        public Scheduler(string workingDirectory, string storeDirectory, string configDirectory)
            : base(workingDirectory)
        {
            StoreDirectory = storeDirectory;
            ConfigDirectory = configDirectory;
        }
        public string StoreDirectory { get; }
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
        protected override (string scheduleId, Schedule schedule) GetFirstScheduleTaskFromStorage()
        {
            throw new NotImplementedException();
        }
        protected override void StartScheduleTaskInStorage(string scheduleId)
        {
            throw new NotImplementedException();
        }
        protected override void UpdateScheduleTaskInStorage(string scheduleId, Schedule schedule)
        {
            throw new NotImplementedException();
        }
        protected override void FinishScheduleTaskInStorage(string scheduleId)
        {
            throw new NotImplementedException();
        }
    }
}
