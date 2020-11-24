using Newtonsoft.Json;
using System;
using System.Linq;

namespace SLiTS.Api
{
    public class Schedule
    {
        [JsonIgnore]
        public string Id { get; set; }
        public string Title { get; set; }
        public DayOfWeek[] WeeklyPlan { get; set; }
        public TimeSpan BeginDailyPlan { get; set; }
        public TimeSpan EndDailyPlan { get; set; }
        public TimeSpan MinimalElapsed { get; set; }
        public bool Active { get; set; }
        public bool Repeat { get; set; }
        public DateTime LastRunning { get; set; }
        public string Parameters { get; set; }
        public string[] UsingResource { get; set; }
        public string TaskHandler { get; set; }
        public bool TestInQueue()
        {
            DateTime now = DateTime.Now;
            return WeeklyPlan.Contains(now.DayOfWeek) && now.TimeOfDay > BeginDailyPlan && now.TimeOfDay < EndDailyPlan;
        }

        public TimeSpan GetRealWaiting()
        {
            return DateTime.Now - LastRunning - MinimalElapsed;
        }
    }
}
