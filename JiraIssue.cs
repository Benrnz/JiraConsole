using BensJiraConsole;

public record JiraIssue(string Key, string Summary, string Status, string Assignee, DateTimeOffset Created, string IssueType)
{
    public string? DevTimeSpent { get; set; }

    public float? StoryPoints { get; set; }

    public string? BugType { get; set; }

    public string? Customers { get; set; }

    public string? Category { get; set; }
}

public record JiraIssueWithPmPlan(string Key, string Summary, string Status, string Assignee, DateTimeOffset Created, string IssueType)
    : JiraIssue(Key, Summary, Status, Assignee, Created, IssueType)
{
    public JiraPmPlan PmPlan { get; set; }
}
