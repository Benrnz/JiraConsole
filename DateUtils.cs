namespace BensEngineeringMetrics;

public static class DateUtils
{
    /// <summary>
    ///     Calculates a start date based on a target start date and DateTime.Today. The target date may be shifted back to ensure today's date is included as the last date in a weekly data set.
    ///     This is so the resulting date starts a week prior to today's date, and resulting data with rows for weeks includesf  today's date.
    /// </summary>
    /// <example>
    ///     Examples:
    ///     If today is Wednesday (3):
    ///     * Desired day becomes Tuesday (2)
    ///     * If target date is Friday (5), it will move back 3 days to Tuesday
    ///     * If target date is Monday (1), it will move back 6 days to previous Tuesday
    ///     If today is Monday (1):
    ///     * Desired day becomes Sunday (0)
    ///     * If target date is Thursday (4), it will move back 4 days to Sunday
    ///     * If target date is Saturday (6), it will move back 6 days to Sunday
    /// </example>
    public static DateTimeOffset FindBestStartDateForWeeklyData(DateTimeOffset targetDate)
    {
        var todayDayOfWeek = (int)new DateTimeOffset(DateTime.Today).DayOfWeek;
        var desiredDayOfWeek = (todayDayOfWeek - 1 + 7) % 7;
        var targetDayOfWeek = (int)targetDate.DayOfWeek;
        var daysToSubtract = (targetDayOfWeek - desiredDayOfWeek + 7) % 7;
        return targetDate.AddDays(-daysToSubtract);
    }

    public static DateTimeOffset StartOfMonth(DateTimeOffset dateTime)
    {
        return new DateTimeOffset(dateTime.Year, dateTime.Month, 1, 0, 0, 0, dateTime.Offset);
    }

    public static DateTimeOffset EndOfMonth(DateTimeOffset dateTime)
    {
        var startOfNextMonth = StartOfMonth(dateTime).AddMonths(1);
        return startOfNextMonth.AddTicks(-1);
    }
}
