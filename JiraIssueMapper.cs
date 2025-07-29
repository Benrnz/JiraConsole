using System.Text.Json;

namespace BensJiraConsole;

public class JiraIssueMapper
{
    private readonly JsonSerializerOptions options = new();

    public JiraIssueMapper()
    {
        this.options.Converters.Add(new CustomDateTimeOffsetConverter());
    }

    public List<JiraPmPlan> MapToPmPlan(string json)
    {
        var dto = JsonSerializer.Deserialize<JiraResponseDto>(json, this.options);
        var output = new List<JiraPmPlan>();
        if (dto == null || dto.Issues.Count == 0)
        {
            return output;
        }

        foreach (var issue in dto.Issues)
        {
            var required = issue.Fields.IsRequiredForGoLive ?? 0;
            var jiraIdea = new JiraPmPlan(
                issue.Key,
                issue.Fields.Summary,
                Math.Abs(required - 1) < 0.1,
                issue.Fields.EstimationStatus?.Description ?? "Unknown",
                issue.Fields.PmPlanHighLevelEstimate ?? 0
            );

            output.Add(jiraIdea);
        }

        if (!dto.IsLastPage)
        {
            Console.WriteLine($"WARNING! Too many issues found. Only the first {dto.Issues.Count} are exported.");
        }
        return output;
    }

    public List<JiraIssue> MapToJiraIssue(string responseJson)
    {
        var dto = JsonSerializer.Deserialize<JiraResponseDto>(responseJson, this.options);

        var output = new List<JiraIssue>();
        if (dto == null || dto.Issues.Count == 0)
        {
            return output;
        }

        foreach (var issue in dto.Issues)
        {
            var jiraIssue = new JiraIssue(
                issue.Key,
                issue.Fields.Summary,
                issue.Fields.Status.Name,
                issue.Fields.Assignee?.DisplayName ?? "Unassigned",
                issue.Fields.Created
            );
            output.Add(jiraIssue);
        }

        if (!dto.IsLastPage)
        {
            Console.WriteLine($"WARNING! Too many issues found. Only the first {dto.Issues.Count} are exported.");
        }

        return output;
    }
}
