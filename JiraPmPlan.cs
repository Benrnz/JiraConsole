namespace BensJiraConsole;

public record JiraPmPlan(string Key, string Summary, bool RequiredForGoLive, string EstimationStatus, float PmPlanHighLevelEstimate);
