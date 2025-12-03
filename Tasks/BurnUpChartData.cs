namespace BensEngineeringMetrics.Tasks;

public class BurnUpChartData
{
    public DateTime Date { get; set; }

    public double? TotalDaysEffort { get; set; }

    public double? TotalDaysEffortTrend { get; set; }

    public double? WorkCompleted { get; set; }

    public double? WorkCompletedTrend { get; set; }
}
