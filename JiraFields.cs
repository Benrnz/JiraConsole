namespace BensJiraConsole;

public static class JiraFields
{
    //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
    public static readonly FieldMapping IssueType = new("issuetype", "IssueType", "name");
    public static readonly FieldMapping Status = new("status", "Status", "name");
    public static readonly FieldMapping StoryPoints = new("customfield_10004", "StoryPoints");
    public static readonly FieldMapping Created = new("created");
    public static readonly FieldMapping Resolved = new("resolutiondate", "Resolved");
    public static readonly FieldMapping Summary = new("summary", "Summary");
    public static readonly FieldMapping ParentKey = new("parent", "Parent", "key");
    public static readonly FieldMapping OriginalEstimate = new("timeoriginalestimate", "Original Estimate");
    public static readonly FieldMapping AssigneeDisplay = new("assignee", "Assignee", "displayName");
    public static readonly FieldMapping ReporterDisplay = new("reporter", "Reporter", "displayName");
    public static readonly FieldMapping Priority = new("priority", "Priority", "name");
    public static readonly FieldMapping DueDate = new("duedate", "Due Date");
    public static readonly FieldMapping Updated = new("updated", "Updated");
    public static readonly FieldMapping Sprint = new("customfield_10007", "Sprint");
    public static readonly FieldMapping EpicLink = new("customfield_10008", "Epic Link");
    public static readonly FieldMapping FixVersions = new("fixVersions", "Fix Versions");
    public static readonly FieldMapping Components = new("components", "Components");
    public static readonly FieldMapping Labels = new("labels", "Labels");
    public static readonly FieldMapping Description = new("description", "Description");
    public static readonly FieldMapping Project = new("project", "Project", "key");
    public static readonly FieldMapping Resolution = new("resolution", "Resolution", "name");
    public static readonly FieldMapping Environment = new("environment", "Environment");
    public static readonly FieldMapping TimeSpent = new("timespent", "Time Spent");
    public static readonly FieldMapping Progress = new("progress", "Progress");
    public static readonly FieldMapping AggregateProgress = new("aggregateprogress", "Aggregate Progress");
    public static readonly FieldMapping Worklog = new("worklog", "Worklog");
    // Add any other unique FieldMapping instances found in your codebase here
}
