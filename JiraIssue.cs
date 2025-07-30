using BensJiraConsole;

public record JiraIssue(string Key, string Summary, string Status, string Assignee, DateTimeOffset Created, string IssueType)
{
    public JiraPmPlan PmPlan { get; set; }

    public string? DevTimeSpent { get; set; }

    public float? StoryPoints { get; set; }
}
