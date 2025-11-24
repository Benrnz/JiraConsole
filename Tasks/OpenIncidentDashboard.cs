using System.Diagnostics.CodeAnalysis;
using BensJiraConsole.Jira;

namespace BensJiraConsole.Tasks;

public class OpenIncidentDashboard(IJiraQueryRunner runner, IWorkSheetUpdater sheetUpdater, ISlackClient slack) : IJiraExportTask
{
    private const string TaskKey = "INCIDENTS";
    private const string JavPmGoogleSheetId = "16bZeQEPobWcpsD8w7cI2ftdSoT1xWJS8eu41JTJP-oI";
    private const string OtPmGoogleSheetId = "14Dqa1UVXQJrAViBHgbS8kHBmHi61HnkZAKa6wCsTL2E";
    private const string GoogleSheetTabName = "Open Incidents Dashboard";
    private const string NoSprintAssigned = "No Sprint";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Status,
        JiraFields.Summary,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.CustomersMultiSelect,
        JiraFields.Sprint,
        JiraFields.Severity,
        JiraFields.UpdatedDate
    ];

    private IReadOnlyList<SlackChannel> incidentSlackChannels = new List<SlackChannel>();

    private List<IList<object?>> sheetData = new();

    public string Description => "Pulls data from Jira and Slack to give a combined view of all open incidents.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine("Updating Incident Dashboard for JAVPM...");
        await RunReportForProject(Constants.JavPmJiraProjectKey, JavPmGoogleSheetId);
        Console.WriteLine("Updating Incident Dashboard for OTPM...");
        await RunReportForProject(Constants.OtPmJiraProjectKey, OtPmGoogleSheetId);
    }

    private void CreateTableForOpenTicketSummary(IReadOnlyList<JiraIssue> jiraIssues)
    {
        Console.WriteLine("Creating table for open ticket summary...");

        // Row 1
        this.sheetData.Add([null, "Number of P1s", "Number of P2s"]);
        sheetUpdater.BoldCells(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 3);
        // Row 2
        this.sheetData.Add([
            "Total Open Tickets:",
            jiraIssues.Count(i => i.Severity == Constants.SeverityCritical),
            jiraIssues.Count(i => i.Severity == Constants.SeverityMajor)
        ]);
        sheetUpdater.BoldCells(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 1);
        // Row 3
        this.sheetData.Add([
            "In Sprint:",
            jiraIssues.Count(i => i.Severity == Constants.SeverityCritical && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != NoSprintAssigned),
            jiraIssues.Count(i => i.Severity == Constants.SeverityMajor && !string.IsNullOrWhiteSpace(i.Sprint) && i.Sprint != NoSprintAssigned)
        ]);

        // Group by customer
        var customerTickets = new List<CustomerTickets>();
        foreach (var customer in GetUniqueCustomerList(jiraIssues).Where(c => c != Constants.Javln && c != string.Empty))
        {
            var group = jiraIssues.Where(i => i.CustomerArray.Contains(customer)).ToList();

            var p1Issues = group.Count(i => i.Severity == Constants.SeverityCritical);
            var p2Issues = group.Count(i => i.Severity == Constants.SeverityMajor);
            if (p1Issues == 0 && p2Issues == 0)
            {
                continue;
            }

            customerTickets.Add(new CustomerTickets(customer, p1Issues, p2Issues, group.ToArray()));
        }

        // Row 4+
        var rank = 1;
        foreach (var customer in customerTickets.OrderByDescending(c => c.P1Count).ThenByDescending(c => c.P2Count))
        {
            this.sheetData.Add([$"{rank++}) {customer.CustomerName}", customer.P1Count, customer.P2Count]);
            if (rank > 5)
            {
                break;
            }
        }

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }

    private void CreateTableForPriorityBugList(IReadOnlyList<JiraIssue> jiraIssues, string severity)
    {
        var priorityName = severity == Constants.SeverityCritical ? "P1" : "P2";
        Console.WriteLine($"Creating table for {priorityName} list...");

        this.sheetData.Add([$"List of Open {priorityName}s", "Status", "Customer", "Summary", "Sprint", "Last Activity (days ago)"]);
        sheetUpdater.BoldCells(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 6);
        foreach (var issue in jiraIssues.Where(i => !i.Customers.Contains(Constants.Javln) && i.Severity == severity).OrderByDescending(i => i.LastActivity))
        {
            this.sheetData.Add([
                $"=HYPERLINK(\"https://javlnsupport.atlassian.net/browse/{issue.Key}\", \"{issue.Key}\")",
                issue.Status,
                issue.Customers,
                issue.Summary,
                issue.Sprint,
                issue.LastActivity
            ]);
        }

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }

    private async Task CreateTableForSlackChannels()
    {
        Console.WriteLine("Creating table for Slack Channel Incidents...");
        if (!this.incidentSlackChannels.Any())
        {
            this.incidentSlackChannels = await slack.FindAllChannels("incident-");
        }

        this.sheetData.Add(["Open Slack Incident-* Channels", null, "Last Message (days ago)"]);
        await sheetUpdater.BoldCells(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 3);
        this.sheetData.Add([$"{this.incidentSlackChannels.Count} Incident channels open"]);
        foreach (var channel in this.incidentSlackChannels)
        {
            var daysAgo = channel.LastMessageTimestamp.HasValue
                ? (int)(DateTimeOffset.Now - channel.LastMessageTimestamp.Value).TotalDays
                : (int?)null;
            var daysAgoText = daysAgo?.ToString() ?? "N/A";
            this.sheetData.Add([$"=HYPERLINK(\"https://javln.slack.com/archives/{channel.Id}\", \"{channel.Name}\")", null, daysAgoText]);
        }

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private async Task CreateTableForTeamVelocity(string project)
    {
        Console.WriteLine("Creating table for team velocity...");
        this.sheetData.Add(["Team Velocity (Avg Last 5 sprints)", "P1s Avg", "P2s Avg", "Other Avg"]);
        await sheetUpdater.BoldCells(GoogleSheetTabName, this.sheetData.Count - 1, this.sheetData.Count, 0, 4);

        var teamData = new List<(string, int, int, int)>();
        foreach (var team in JiraConfig.Teams.Where(t => t.JiraProject == project))
        {
            var last5Sprints = (await runner.GetAllSprints(team.BoardId))
                .Where(t => t.State == Constants.SprintStateClosed)
                .OrderByDescending(t => t.StartDate)
                .Take(5);
            var p1Count = 0;
            var p2Count = 0;
            var otherCount = 0;
            foreach (var sprint in last5Sprints)
            {
                var tickets = (await runner.SearchJiraIssuesWithJqlAsync($"sprint = {sprint.Id} AND Severity IN (Critical, Major, Intermediate)", [JiraFields.Severity])).ToList();
                var p1s = tickets.Count(t => t.Severity == Constants.SeverityCritical);
                p1Count += p1s;
                var p2s = tickets.Count(t => t.Severity == Constants.SeverityMajor);
                p2Count += p2s;
                otherCount += tickets.Count - p1s - p2s;
            }

            teamData.Add((team.TeamName, p1Count / 5, p2Count / 5, otherCount / 5));
        }

        this.sheetData.Add(["Avg across all teams", teamData.Sum(d => d.Item2), teamData.Sum(d => d.Item3), teamData.Sum(d => d.Item4)]);
        this.sheetData.AddRange(teamData
            .OrderByDescending(t => t.Item2)
            .Select(t => (IList<object?>)new List<object?> { t.Item1, t.Item2, t.Item3, t.Item4 }));

        this.sheetData.Add([]);
        this.sheetData.Add([]);
    }

    private IOrderedEnumerable<string> GetUniqueCustomerList(IReadOnlyList<JiraIssue> jiraIssues)
    {
        return jiraIssues
            .SelectMany(i => i.CustomerArray)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c);
    }

    private async Task<IReadOnlyList<JiraIssue>> RetrieveJiraData(string project)
    {
        var jql = $"project = {project} AND issueType = Bug AND status != Done";
        var issues = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(JiraIssue.CreateJiraIssue);

        return issues.ToList();
    }

    private async Task RunReportForProject(string project, string sheetId)
    {
        this.sheetData = new List<IList<object?>>();
        await sheetUpdater.Open(sheetId);
        sheetUpdater.ClearRange(GoogleSheetTabName, "A2:Z10000");
        await sheetUpdater.ClearRangeFormatting(GoogleSheetTabName, 1, 10000, 0, 26);

        SetLastUpdateTime();
        var jiraIssues = await RetrieveJiraData(project);
        CreateTableForOpenTicketSummary(jiraIssues);
        await CreateTableForTeamVelocity(project);
        await CreateTableForSlackChannels();
        CreateTableForPriorityBugList(jiraIssues, Constants.SeverityCritical);
        CreateTableForPriorityBugList(jiraIssues, Constants.SeverityMajor);

        sheetUpdater.EditSheet($"{GoogleSheetTabName}!A1", this.sheetData, true);
        await sheetUpdater.SubmitBatch();
    }

    private void SetLastUpdateTime()
    {
        var row = new List<object?> { null, DateTime.Now.ToString("d-MMM-yy HH:mm") };
        this.sheetData.Add(row);
        this.sheetData.Add(new List<object?>());
    }

    private record JiraIssue(
        string Key,
        string Summary,
        string Sprint,
        string Customers,
        string[] CustomerArray,
        string Severity,
        string Team,
        string Status,
        double LastActivity)
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            var customer = JiraFields.CustomersMultiSelect.Parse(d) ?? string.Empty;
            var lastUpdatedDate = (DateTimeOffset?)JiraFields.UpdatedDate.Parse(d) ?? DateTimeOffset.MaxValue;
            var lastUpdatedDaysAgo = (DateTimeOffset.Now - lastUpdatedDate).TotalDays;
            var sprint = (string)JiraFields.Sprint.Parse(d);
            return new JiraIssue(
                Key: JiraFields.Key.Parse(d),
                Summary: JiraFields.Summary.Parse(d),
                Sprint: string.IsNullOrWhiteSpace(sprint) ? NoSprintAssigned : sprint,
                Customers: customer,
                CustomerArray: customer.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Severity: JiraFields.Severity.Parse(d) ?? string.Empty,
                Team: JiraFields.Team.Parse(d) ?? "No Team",
                Status: JiraFields.Status.Parse(d),
                LastActivity: lastUpdatedDaysAgo < 0 ? 999 : Math.Round(lastUpdatedDaysAgo, 1)
            );
        }
    }

    private record CustomerTickets(string CustomerName, int P1Count, int P2Count, JiraIssue[] Tickets);
}
