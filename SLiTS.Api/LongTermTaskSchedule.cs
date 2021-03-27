using Newtonsoft.Json;
using System;
using System.Linq;

namespace SLiTS.Api
{
    public class LongTermTaskSchedule
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
        public string ClassHandler { get; set; }
        public bool TestInQueue()
        {
            DateTime now = DateTime.Now;
            return GetRealWaiting() > TimeSpan.Zero && WeeklyPlan.Contains(now.DayOfWeek) && now.TimeOfDay > BeginDailyPlan && now.TimeOfDay < EndDailyPlan;
        }
        public TimeSpan GetRealWaiting() => DateTime.Now - LastRunning - MinimalElapsed;
        public override string ToString()
        {
            return @$"
[ Id: ""{Id}"",
  Title: ""{Title}"",
  WeeklyPlan: ""{WeeklyPlan.Select(d => d.ToString()).Aggregate((cur, next) => cur + ", " + next)}"",
  BeginDailyPlan: ""{BeginDailyPlan}"",
  EndDailyPlan: ""{EndDailyPlan}"",
  MinimalElapsed: ""{MinimalElapsed}"",
  Active: ""{Active}"",
  Repeat: ""{Repeat}"",
  LastRunning: ""{LastRunning}"",
  Parameters: ""{Parameters}"",
  UsingResource: ""{UsingResource.Aggregate((cur, next) => cur + $", \"{next}")}"",
  TaskHandler: ""{ClassHandler}"" ]";
        }
    }
}
