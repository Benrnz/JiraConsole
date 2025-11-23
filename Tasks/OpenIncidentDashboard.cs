namespace BensJiraConsole.Tasks;

public class OpenIncidentDashboard(IJiraQueryRunner runner, IWorkSheetUpdater sheetUpdater) : IJiraExportTask
{
    private const string TaskKey = "INCIDENTS";
    private const string GoogleSheetId = "1M5ftE2dtQ1l-NoSL6sqkLynVOpHFeixVBF0faOzefvw";
    private const string GoogleSheetTabName = "Open Incident Dashboard";
    private const string NoSprintAssigned = "No Sprint";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Status,
        JiraFields.Summary,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.CustomersMultiSelect,
        JiraFields.Sprint,
        JiraFields.Severity
    ];

    private readonly List<IList<object?>> sheetData = new();

    public string Description => "Pulls data from Jira and Slack to give a combined view of all open incidents.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.ClearRange(GoogleSheetTabName, "A2:Z10000");
        await sheetUpdater.ClearRangeFormatting(GoogleSheetTabName, 0, 10000, 0, 26);

        SetLastUpdateTime();
        var jiraIssues = await RetrieveJiraData(Constants.JavPmJiraProjectKey);
        CreateTableForOpenTicketSummary(jiraIssues);
        CreateTableForTeamVelocity(jiraIssues);

        sheetUpdater.EditSheet($"{GoogleSheetTabName}!A1", this.sheetData, true);
        await sheetUpdater.SubmitBatch();
    }

    private void CreateTableForOpenTicketSummary(IReadOnlyList<JiraIssue> jiraIssues)
    {
        // Row 1
        this.sheetData.Add([null, "Number if P1s", "Number of P2s"]);
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

    private void CreateTableForTeamVelocity(IReadOnlyList<JiraIssue> jiraIssues)
    {
        this.sheetData.Add(["Team Velocity (Last 5 sprints)", "P1s", "P2s", "Other"]);
        this.sheetData.Add(["Total"]);


        // TODO
        // How to find a list of sprint numbers for a team?
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
        // TODO Likely need to calc last days activity here
        return issues.ToList();
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
        int LastActivity)
    {
        public static JiraIssue CreateJiraIssue(dynamic d)
        {
            var customer = JiraFields.CustomersMultiSelect.Parse(d) ?? string.Empty;
            return new JiraIssue(
                Key: JiraFields.Key.Parse(d),
                Summary: JiraFields.Summary.Parse(d),
                Sprint: JiraFields.Sprint.Parse(d) ?? NoSprintAssigned,
                Customers: customer,
                CustomerArray: customer.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                Severity: JiraFields.Severity.Parse(d) ?? string.Empty,
                Team: JiraFields.Team.Parse(d) ?? "No Team",
                Status: JiraFields.Status.Parse(d),
                LastActivity: 999 //TODO
            );
        }
    }

    private record CustomerTickets(string CustomerName, int P1Count, int P2Count, JiraIssue[] Tickets);
}
