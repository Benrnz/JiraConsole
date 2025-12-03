using System.Dynamic;

namespace BensEngineeringMetrics.Jira;

public static class JiraFields
{
    //  JIRA Field Name,          Friendly Alias,                    Flatten object field name
    public static readonly FieldMapping<string> AssigneeDisplay = new() { Field = "assignee", Alias = "Assignee", FlattenField = "displayName" };
    public static readonly FieldMapping<string> BugType = new() { Field = "customfield_11903", Alias = "BugType", FlattenField = "value" };
    public static readonly FieldMapping<string> Category = new() { Field = "customfield_11906", Alias = "Category", FlattenField = "value" };
    public static readonly FieldMapping<string> CodeAreaParent = new() { Field = "customfield_12605", Alias = "CodeAreaParent", FlattenField = "value" };
    public static readonly FieldMapping<string> CodeArea = new() { Field = "customfield_12604", Alias = "CodeArea", FlattenField = "value" };
    public static readonly FieldMapping<DateTimeOffset> Created = new() { Field = "created", Alias = "Created" };
    public static readonly FieldMapping<string> CustomersMultiSelect = new() { Field = "customfield_11812", Alias = "CustomersMultiSelect", FlattenField = "value" };
    public static readonly FieldMapping<string> DevTimeSpent = new FieldMappingWithParser<string> { Field = "customfield_11934", Alias = "DevTimeSpent", Parser = ParseDevTimeSpent };
    public static readonly FieldMapping<string> EstimationStatus = new() { Field = "customfield_12137", Alias = "EstimationStatus", FlattenField = "value" };
    public static readonly FieldMapping<int> FlagCount = new FieldMappingWithParser<int> { Field = "customfield_12236", Alias = "FlagCount", Parser = ParseFlagCount };
    public static readonly FieldMapping<bool> IsReqdForGoLive = new FieldMappingWithParser<bool> { Field = "customfield_11986", Alias = "IsReqdForGoLive", Parser = ParseIsReqdForGoLive };
    public static readonly FieldMapping<string> IssueType = new() { Field = "issuetype", Alias = "IssueType", FlattenField = "name" };
    public static readonly FieldMapping<string> Key = new FieldMappingWithParser<string> { Field = "key", Alias = "Key", Parser = ParseKey };
    public static readonly FieldMapping<long> OriginalEstimate = new() { Field = "timeoriginalestimate", Alias = "OriginalEstimate" };
    public static readonly FieldMapping<string> ParentKey = new() { Field = "parent", Alias = "Parent", FlattenField = "key" };
    public static readonly FieldMapping<double> PmPlanHighLevelEstimate = new() { Field = "customfield_12038", Alias = "PmPlanHighLevelEstimate" };
    public static readonly FieldMapping<string> PmPlanCustomer = new() { Field = "customfield_11983", Alias = "PmPlanCustomer", FlattenField = "value" };
    public static readonly FieldMapping<string> Priority = new() { Field = "priority", Alias = "Priority", FlattenField = "name" };

    public static readonly FieldMapping<DateTimeOffset?> ProjectTarget = new FieldMappingWithParser<DateTimeOffset?>
        { Field = "customfield_11975", Alias = "ProjectTarget", Parser = ParseProjectTarget };

    public static readonly FieldMapping<string> ReporterDisplay = new() { Field = "reporter", Alias = "Reporter", FlattenField = "displayName" };
    public static readonly FieldMapping<string> Resolution = new() { Field = "resolution", Alias = "Resolution", FlattenField = "name" };
    public static readonly FieldMapping<DateTimeOffset> Resolved = new() { Field = "resolutiondate", Alias = "Resolved" };
    public static readonly FieldMapping<string> Severity = new() { Field = "customfield_11899", Alias = "Severity", FlattenField = "value" };
    public static readonly FieldMapping<string> Sprint = new FieldMappingWithParser<string> { Field = "customfield_10007", Alias = "Sprint", FlattenField = "name", Parser = ParseSprintName };

    public static readonly FieldMapping<DateTimeOffset> SprintStartDate = new FieldMappingWithParser<DateTimeOffset>
        { Field = "customfield_10007", Alias = "SprintStartDate", FlattenField = "startDate", Parser = ParseSprintStartDate };

    public static readonly FieldMapping<string> Status = new() { Field = "status", Alias = "Status", FlattenField = "name" };
    public static readonly FieldMapping<double> StoryPoints = new() { Field = "customfield_10004", Alias = "StoryPoints" };
    public static readonly FieldMapping<string> Summary = new() { Field = "summary", Alias = "Summary" };
    public static readonly FieldMapping<string> Team = new() { Field = "customfield_11400", Alias = "Team", FlattenField = "name" };
    public static readonly FieldMapping<DateTimeOffset> UpdatedDate = new() { Field = "updated", Alias = "UpdatedDate" };

    private static bool DynamicHasField(dynamic d, string fieldName)
    {
        return ((ExpandoObject)d).Any(e => e.Key == fieldName);
    }

    private static (DateTimeOffset?, string) LatestSprint(dynamic d)
    {
        // Sprint Start Date may not be in the data set at all.
        Func<dynamic, string> getSprintDates;
        if (DynamicHasField(d, "SprintStartDate"))
        {
            getSprintDates = d1 => d1.SprintStartDate ?? string.Empty;
        }
        else
        {
            getSprintDates = _ => string.Empty;
        }

        string sprintNames = d.Sprint ?? string.Empty;
        string sprintDates = getSprintDates(d);

        if (!sprintNames.Contains(',') || !sprintDates.Contains(','))
        {
            // Data does not contain multiple sprints.
            if (!DateTimeOffset.TryParse(getSprintDates(d), out DateTimeOffset startDate))
            {
                startDate = DateTimeOffset.MaxValue;
            }

            return (startDate, sprintNames);
        }

        var sprintNameList = sprintNames.Split(',');
        var sprintDateList = sprintDates.Split(',');
        List<Tuple<DateTimeOffset?, string>> sprintDateAndName = new();
        for (var i = 0; i < sprintNameList.Length; i++)
        {
            if (i < sprintDateList.Length)
            {
                if (DateTimeOffset.TryParse(sprintDateList[i], out var sprintDateParsed))
                {
                    sprintDateAndName.Add(new Tuple<DateTimeOffset?, string>(sprintDateParsed, sprintNameList[i]));
                }
            }
            else
            {
                sprintDateAndName.Add(new Tuple<DateTimeOffset?, string>(DateTimeOffset.MaxValue, sprintNameList[i]));
            }
        }

        var latestSprint = sprintDateAndName.OrderByDescending(t => t.Item1).First();
        return (latestSprint.Item1, latestSprint.Item2);
    }

    private static string? ParseDevTimeSpent(dynamic d)
    {
        // DevTimeSpent has come through as a DateTimeOffset in real data and also a string.
        if (d.DevTimeSpent is string stringTime)
        {
            return stringTime;
        }

        if (d.DevTimeSpent is DateTimeOffset dateTime)
        {
            return dateTime.ToString("d");
        }

        if (d.DevTimeSpent is null)
        {
            return null;
        }

        throw new NotSupportedException($"DevTimeSpent data type {d.DevTimeSpent.GetType().Name} is not supported");
    }

    private static int ParseFlagCount(dynamic d)
    {
        if (d.FlagCount is null)
        {
            return 0;
        }

        if (d.FlagCount is double dbl)
        {
            return Convert.ToInt32(dbl);
        }

        if (d.FlagCount is float f)
        {
            return Convert.ToInt32(f);
        }

        throw new NotSupportedException("Incorrect data type for FlagCount.");
    }

    private static bool ParseIsReqdForGoLive(dynamic d)
    {
        // IsReqdForGoLive is a number in the real data, but can be a string in some cases or null.
        if (d.IsReqdForGoLive is string str)
        {
            return double.TryParse(str, out var result) && result > 0.01;
        }

        if (d.IsReqdForGoLive is double dbl)
        {
            return dbl > 0.01;
        }

        if (d.IsReqdForGoLive is bool boolvalue)
        {
            return boolvalue;
        }

        if (d.IsReqdForGoLive is null)
        {
            return false;
        }

        throw new NotSupportedException($"IsReqdForGoLive data type {d.IsReqdForGoLive.GetType().Name} is not supported");
    }

    private static string ParseKey(dynamic d)
    {
        if (d is IDictionary<string, object> dictionary && dictionary.TryGetValue("key", out var value))
        {
            if (value is null)
            {
                throw new NotSupportedException("Key cannot be null - likely bug in app.");
            }

            return (string)value;
        }

        throw new NotSupportedException("This is caused by requesting Key as a Field, you don't need to do this. Key is always included, remove it from the Field List.");
    }

    private static DateTimeOffset? ParseProjectTarget(dynamic d)
    {
        // Comes thru as a string: {"start":"2025-09-05","end":"2025-09-05"}
        var value = d.ProjectTarget;
        if (value is null)
        {
            return null;
        }

        if (value is string stringValue)
        {
            var start = stringValue.IndexOf(":\"", StringComparison.Ordinal);
            var dateCandidate = stringValue.Substring(start + 2, 10);
            return DateTimeOffset.Parse(dateCandidate);
        }

        return null;
    }

    private static string ParseSprintName(dynamic d)
    {
        if (!DynamicHasField(d, "Sprint"))
        {
            return string.Empty;
        }

        if (d.Sprint is null)
        {
            return string.Empty;
        }

        var lastSprint = LatestSprint(d);
        if (lastSprint.Item1 is null)
        {
            return lastSprint.Item2;
        }

        return lastSprint.Item2;
    }

    private static DateTimeOffset ParseSprintStartDate(dynamic d)
    {
        // SprintStartDate could be multiple for example "2025-08-09T01:01:01.000+00:00,2025-08-23T01:01:01.000+00:00"
        // or could be one date or could be null.
        // This parser will return max date if null, and the last date if there are multiple.

        if (!DynamicHasField(d, "SprintStartDate"))
        {
            return DateTimeOffset.MaxValue;
        }

        if (d.SprintStartDate is null)
        {
            return DateTimeOffset.MaxValue;
        }

        var lastSprint = LatestSprint(d);
        if (lastSprint.Item1 is null)
        {
            return DateTimeOffset.MaxValue;
        }

        return lastSprint.Item1!;
    }
}
