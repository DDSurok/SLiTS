using System;

namespace SLiTS.Api
{
    public class Schedule
    {
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
    }
}
