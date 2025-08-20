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
    public static readonly FieldMapping OriginalEstimate = new("timeoriginalestimate", "OriginalEstimate");
    public static readonly FieldMapping AssigneeDisplay = new("assignee", "Assignee", "displayName");
    public static readonly FieldMapping ReporterDisplay = new("reporter", "Reporter", "displayName");
    public static readonly FieldMapping Priority = new("priority", "Priority", "name");
    public static readonly FieldMapping Sprint = new("customfield_10007", "Sprint");
    public static readonly FieldMapping Resolution = new("resolutiondate", "Resolved", "name");
    public static readonly FieldMapping PmPlanHighLevelEstimate = new("customfield_12038", "PmPlanHighLevelEstimate");
    public static readonly FieldMapping EstimationStatus = new("customfield_12137", "EstimationStatus", "value");
    public static readonly FieldMapping IsReqdForGoLive = new("customfield_11986", "IsReqdForGoLive");
    public static readonly FieldMapping DevTimeSpent = new("customfield_11934", "DevTimeSpent");
    public static readonly FieldMapping BugType = new("customfield_11903", "BugType", "value");
    public static readonly FieldMapping CustomersMultiSelect = new("customfield_11812", "CustomersMultiSelect", "value");
    public static readonly FieldMapping Category = new("customfield_11906", "Category", "value");
    public static readonly FieldMapping FlagCount = new("customfield_12236", "FlagCount");
    public static readonly FieldMapping Severity = new("customfield_11899", "Severity", "value");

    public static readonly FieldMapping Team = new("customfield_11400", "Team", "name");
    // Add any other unique FieldMapping instances found in your codebase here
}
