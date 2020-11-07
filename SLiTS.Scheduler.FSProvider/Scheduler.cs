using SLiTS.Api;
using System;
using System.Collections.Generic;

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

        public override Schedule ReadAsync()
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<(string handler, string title, string @params)> ReadFastTaskParams()
        {
            throw new NotImplementedException();
        }
    }
}
