﻿using System.Text.Json;

namespace BensJiraConsole;

public class JiraIssueMapper
{
    private readonly JsonSerializerOptions options = new();

    public JiraIssueMapper()
    {
        this.options.Converters.Add(new CustomDateTimeOffsetConverter());
    }

    public bool WasLastPage { get; private set; }

    public string? NextPageToken { get; private set; }

    public List<JiraPmPlan> MapToPmPlan(string responseJson)
    {
        var dto = JsonSerializer.Deserialize<JiraResponseDto>(responseJson, this.options);
        var output = new List<JiraPmPlan>();
        var wasLastIndicatorSent = responseJson.Contains("isLast");
        if (dto == null || dto.Issues.Count == 0)
        {
            WasLastPage = true;
            return output;
        }

        foreach (var issue in dto.Issues)
        {
            var required = issue.Fields.IsRequiredForGoLive ?? 0;
            var jiraIdea = new JiraPmPlan(
                issue.Key,
                issue.Fields.Summary,
                issue.Fields.Status?.Name ?? "Unknown",
                Math.Abs(required - 1) < 0.1,
                issue.Fields.EstimationStatus?.Description ?? "Unknown",
                issue.Fields.PmPlanHighLevelEstimate ?? 0
            );

            output.Add(jiraIdea);
        }

        NextPageToken = dto.NextPageToken;
        if (!wasLastIndicatorSent)
        {
            Console.WriteLine("    WARNING! The response did not contain an 'isLast' indicator. Assuming this is the last page.");
            WasLastPage = true;
        }
        else
        {
            WasLastPage = dto.IsLastPage;
        }

        if (!WasLastPage)
        {
            Console.WriteLine($"    WARNING! Too many issues found. Only the first {dto.Issues.Count} are exported.");
        }

        return output;
    }

    public List<JiraIssue> MapToJiraIssue(string responseJson, Func<string, string, string, string, DateTimeOffset, string, JiraIssue>? constructor = null)
    {
        var dto = JsonSerializer.Deserialize<JiraResponseDto>(responseJson, this.options);
        var wasLastIndicatorSent = responseJson.Contains("isLast");

        var output = new List<JiraIssue>();
        if (dto == null || dto.Issues.Count == 0)
        {
            WasLastPage = true;
            return output;
        }

        if (constructor == null)
        {
            constructor = (a, b, c, d, e, f) => new JiraIssue(a, b, c, d, e, f);
        }

        foreach (var issue in dto.Issues)
        {
            var jiraIssue = constructor(
                issue.Key,
                issue.Fields.Summary,
                issue.Fields.Status?.Name ?? "Unknown",
                issue.Fields.Assignee?.DisplayName ?? "Unassigned",
                issue.Fields.Created,
                issue.Fields.IssueType?.Name ?? string.Empty);
            jiraIssue.StoryPoints = issue.Fields.StoryPoints;
            jiraIssue.DevTimeSpent = issue.Fields.DevTimeSpent;
            output.Add(jiraIssue);
        }

        NextPageToken = dto.NextPageToken;
        if (!wasLastIndicatorSent)
        {
            Console.WriteLine("    WARNING! The response did not contain an 'isLast' indicator. Assuming this is the last page.");
            WasLastPage = true;
        }
        else
        {
            WasLastPage = dto.IsLastPage;
        }

        if (!WasLastPage)
        {
            Console.WriteLine($"    WARNING! Too many issues found. Only the first {dto.Issues.Count} are exported.");
        }

        return output;
    }
}
