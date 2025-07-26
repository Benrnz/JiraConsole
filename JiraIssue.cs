public record JiraIssue(string Key, string Summary, string Status, string Assignee)
{
    public string PmPlan { get; set; }
}