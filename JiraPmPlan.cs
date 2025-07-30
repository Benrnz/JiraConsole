namespace BensJiraConsole;

public record JiraPmPlan(string Key, string Summary, string Status, bool RequiredForGoLive, string EstimationStatus, float PmPlanHighLevelEstimate);
