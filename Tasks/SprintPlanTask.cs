namespace BensJiraConsole.Tasks;

public class SprintPlanTask(IJiraQueryRunner runner, ICsvExporter exporter, IWorkSheetUpdater sheetUpdater) : IJiraExportTask
{
    private const string GoogleSheetId = "1iS6iB3EA38SHJgDu8rpMFcouGlu1Az8cntKA52U07xU";
    private const string TaskKey = "SPRINT_PLAN";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Status,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.Sprint,
        JiraFields.SprintStartDate,
        JiraFields.IssueType
    ];

    private static readonly IFieldMapping[] PmPlanFields =
    [
        JiraFields.Summary,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.IsReqdForGoLive
    ];

    /// <summary>
    ///     All PMPLANs
    /// </summary>
    private IReadOnlyList<PmPlanIssue> pmPlans = [];

    /// <summary>
    ///     All tickets in current and future sprints.
    /// </summary>
    private IReadOnlyList<JiraIssue> sprintTickets = [];

    public string Description => "Export to a Google Sheet a over-arching plan of all future sprints for the two project teams.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);

        await RetrieveAllData();
        PopulatePmPlansOnSprintTickets();
        await ExportAllSprintTickets();
        await UpdateSheetSprintMasterPlan();
        await sheetUpdater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);
    }

    private void PopulatePmPlansOnSprintTickets()
    {
        // For each sprint ticket, find its PMPLAN from its parent or linked issues.
        foreach (var ticket in this.sprintTickets)
        {
            var pmPlan = this.pmPlans.FirstOrDefault(p => p.ChildrenStories.Any(c => c.Key == ticket.Key));
            if (pmPlan != null)
            {
                ticket.PmPlan = pmPlan.Key;
                ticket.PmPlanSummary = pmPlan.Summary;
            }
        }
    }
    private async Task ExportAllSprintTickets()
    {
        // Find PMPLAN for each issue if it exists.
        this.sprintTickets.Join(this.pmPlans, i => i.Key, p => p.Key, (i, p) => (Issue: i, PmPlan: p))
            .ToList()
            .ForEach(x =>
            {
                x.Issue.PmPlan = x.PmPlan.Key;
                x.Issue.PmPlanSummary = x.PmPlan.Summary;
            });

        this.sprintTickets = this.sprintTickets.OrderBy(i => i.Team)
            .ThenBy(i => i.SprintStartDate)
            .ThenBy(i => i.Sprint)
            .ThenBy(i => i.PmPlan)
            .ToList();

        // temp save to CSV
        exporter.SetFileNameMode(FileNameMode.Auto, Key + "_FullData");
        var file = exporter.Export(this.sprintTickets);

        // Export to Google Sheets.
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.CsvFilePathAndName = file;
        await sheetUpdater.ClearRange("Data");
        await sheetUpdater.ImportFile("'Data'!A1");
    }

    private async Task RetrieveAllData()
    {
        // Get all PMPLAN tickets
        Console.WriteLine("Extracting PMPLAN tickets...");
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        this.pmPlans = (await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields)).Select(i => new PmPlanIssue(i)).ToList();

        // Get all children of each PMPLAN
        var childrenJql = "type IN (Story, Improvement, Bug, Epic) AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        foreach (var pmPlan in this.pmPlans)
        {
            pmPlan.ChildrenStories = (await runner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.Key), Fields)).Select(i => new JiraIssue(i)).ToList();
            Console.WriteLine($"Fetched {pmPlan.ChildrenStories.Count} children for {pmPlan.Key}");
            pmPlan.TotalStoryPoints = pmPlan.ChildrenStories.Sum(issue => issue.StoryPoints);
            pmPlan.TotalStoryPointsRemaining = pmPlan.TotalStoryPoints - pmPlan.ChildrenStories.Where(issue => issue.Status == Constants.DoneStatus).Sum(issue => issue.StoryPoints);
        }

        // Get all tickets in open and future sprints for the teams.
        var query = """project = "JAVPM" AND "Team[Team]" IN (60412efa-7e2e-4285-bb4e-f329c3b6d417, 1a05d236-1562-4e58-ae88-1ffc6c5edb32) AND (Sprint IN openSprints() OR Sprint IN futureSprints())""";
        Console.WriteLine(query);
        this.sprintTickets = (await runner.SearchJiraIssuesWithJqlAsync(query, Fields)).Select(i => new JiraIssue(i)).ToList();
    }

    private async Task UpdateSheetSprintMasterPlan()
    {
        var groupBySprint = this.sprintTickets
            .GroupBy(i => (i.Team, i.SprintStartDate, i.Sprint, i.PmPlan, i.PmPlanSummary))
            .OrderBy(g => g.Key.Team)
            .ThenBy(g => g.Key.SprintStartDate)
            .ThenBy(g => g.Key.Sprint)
            .Select(g => new
            {
                g.Key.Team,
                StartDate = g.Key.SprintStartDate,
                SprintName = g.Key.Sprint,
                g.Key.PmPlan,
                Summary = g.Key.PmPlanSummary,
                StoryPoints = g.Sum(x => x.StoryPoints),
                Tickets = g.Count()
            })
            .ToList();

        var sheetData = new List<IList<object?>>();
        var teamSprint = string.Empty;
        foreach (var row in groupBySprint.OrderBy(g => g.StartDate).ThenBy(g => g.Team).ThenBy(g => g.SprintName))
        {
            var pmPlanRecord = this.pmPlans.SingleOrDefault(p => p.Key == row.PmPlan);
            if (row.Team + row.SprintName != teamSprint)
            {
                // Sprint header row
                var rowData1 = new List<object?>
                {
                    row.Team,
                    row.SprintName,
                    row.StartDate == DateTimeOffset.MaxValue ? null : row.StartDate.ToString("d-MMM-yy"),
                    null,
                    null,
                    groupBySprint.Where(g => g.StartDate == row.StartDate && g.SprintName == row.SprintName && g.Team == row.Team).Sum(g => g.Tickets),
                    groupBySprint.Where(g => g.StartDate == row.StartDate && g.SprintName == row.SprintName && g.Team == row.Team).Sum(g => g.StoryPoints)
                };
                sheetData.Add(rowData1);
            }

            // Sprint child Data row
            var percentCompleteStartOfSprint = (pmPlanRecord.TotalStoryPoints - pmPlanRecord.TotalStoryPointsRemaining) / pmPlanRecord.TotalStoryPoints <= 0
                ? 1
                : pmPlanRecord.TotalStoryPointsRemaining;
            var percentCompleteEndOfSprint = (pmPlanRecord.TotalStoryPoints - pmPlanRecord.TotalStoryPointsRemaining + row.StoryPoints) / pmPlanRecord.TotalStoryPoints <= 0
                ? 1
                : pmPlanRecord.TotalStoryPointsRemaining;
            var rowData = new List<object?>
            {
                null,
                null,
                null,
                string.IsNullOrEmpty(row.PmPlan) ? "No PMPLAN" : row.PmPlan,
                row.Summary,
                row.Tickets,
                row.StoryPoints,
                pmPlanRecord.TotalStoryPointsRemaining,
                percentCompleteStartOfSprint,
                percentCompleteEndOfSprint
            };
            sheetData.Add(rowData);
            teamSprint = row.Team + row.SprintName;
        }

        await sheetUpdater.Open(GoogleSheetId);
        await sheetUpdater.ClearRange("Sprint-Master-Plan", "A2:Z10000");
        await sheetUpdater.EditSheet("Sprint-Master-Plan!A2", sheetData);
        //await sheetUpdater.ApplyDateFormat("Sprints-PMPlans", 2, "d mmm yy");
    }

    private record PmPlanIssue
    {
        public PmPlanIssue(dynamic issue)
        {
            Key = JiraFields.Key.Parse(issue);
            Summary = JiraFields.Summary.Parse(issue);
        }

        public string Key { get; }
        public string Summary { get; }
        public List<JiraIssue> ChildrenStories { get; set; } = new();
        public double TotalStoryPoints { get; set; }
        public double TotalStoryPointsRemaining { get; set; }
        public double StoryPointsCompleted => TotalStoryPoints - TotalStoryPointsRemaining;
    }

    private record JiraIssue
    {
        public JiraIssue(dynamic issue)
        {
            Key = JiraFields.Key.Parse(issue);
            Sprint = JiraFields.Sprint.Parse(issue) ?? "No Sprint";
            SprintStartDate = JiraFields.SprintStartDate.Parse(issue);
            Team = JiraFields.Team.Parse(issue) ?? "No Team";
            StoryPoints = JiraFields.StoryPoints.Parse(issue) ?? 0.0;
            Status = JiraFields.Status.Parse(issue);
            Type = JiraFields.IssueType.Parse(issue);
        }

        public string Key { get; }
        public string PmPlan { get; set; } = string.Empty;
        public string PmPlanSummary { get; set; } = string.Empty;
        public string Sprint { get; }
        public DateTimeOffset SprintStartDate { get; }
        public string Status { get; }
        public double StoryPoints { get; }
        public string Team { get; }
        public string Type { get; init; }
    }
}
