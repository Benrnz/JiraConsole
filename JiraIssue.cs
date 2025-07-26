using BensJiraConsole;

public record JiraIssue(string Key, string Summary, string Status, string Assignee)
{
    public JiraPmPlan PmPlan { get; set; }
}
