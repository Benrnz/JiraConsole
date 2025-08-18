namespace BensJiraConsole;

public static class DateUtils
{
    public static DateTimeOffset FindBestStartDate(DateTimeOffset targetDate)
    {
        var todayDayOfWeek = (int)new DateTimeOffset(DateTime.Today).DayOfWeek;
        var desiredDayOfWeek = (todayDayOfWeek - 1 + 7) % 7;
        var targetDayOfWeek = (int)targetDate.DayOfWeek;
        var daysToSubtract = (targetDayOfWeek - desiredDayOfWeek + 7) % 7;
        return targetDate.AddDays(-daysToSubtract);
    }
}
