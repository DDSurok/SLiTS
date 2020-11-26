using System;

namespace SLiTS.Api.Throw
{
    public class BaseScheduleException : Exception
    {
        public BaseScheduleException(Schedule schedule, string message = null, Exception innerException = null)
            : base($"При работе с заданием \"{schedule.Id}\" {schedule} произошла ошибка" + $". {message}", innerException) { }
        public BaseScheduleException(string scheduleId, string message = null, Exception innerException = null)
            : base($"При работе с заданием \"{scheduleId}\" произошла ошибка" + $". {message}", innerException) { }
    }
}
