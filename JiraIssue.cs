using BensJiraConsole;

public record JiraIssue(string Key, string Summary, string Status, string Assignee, DateTimeOffset Created)
{
    public JiraPmPlan PmPlan { get; set; }

    public string? DevTimeSpent { get; set; }

    public float? StoryPoints { get; set; }
}
