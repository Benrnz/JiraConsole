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
    ///     All tickets in current and future sprints.
    /// </summary>
    private IReadOnlyList<JiraIssue> allSprintTickets = [];

    /// <summary>
    ///     All PMPLANs
    /// </summary>
    private IReadOnlyList<PmPlanIssue> pmPlans = [];

    public string Description => "Export to a Google Sheet a over-arching plan of all future sprints for the two project teams.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);

        await RetrieveAllData();
        PopulatePmPlansOnSprintTickets();
        await ExportAllSprintTickets();
        await UpdateSheetSprintMasterPlan();
        sheetUpdater.EditSheet("Info!B1", [[DateTime.Now.ToString("g")]]);
        await sheetUpdater.SubmitBatch();
    }

    private async Task ExportAllSprintTickets()
    {
        // Find PMPLAN for each issue if it exists.
        this.allSprintTickets = this.allSprintTickets.OrderBy(i => i.Team)
            .ThenBy(i => i.SprintStartDate)
            .ThenBy(i => i.Sprint)
            .ThenBy(i => i.PmPlan)
            .ToList();

        // temp save to CSV
        exporter.SetFileNameMode(FileNameMode.Auto, Key + "_FullData");
        var file = exporter.Export(this.allSprintTickets);

        // Export to Google Sheets.
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.CsvFilePathAndName = file;
        sheetUpdater.ClearRange("SprintTickets");
        await sheetUpdater.ImportFile("'SprintTickets'!A1");
    }

    private void PopulatePmPlansOnSprintTickets()
    {
        var flattenPmPlanTickets = this.pmPlans.SelectMany(x => x.ChildrenStories);
        this.allSprintTickets.Join(flattenPmPlanTickets, i => i.Key, p => p.Key, (i, p) => (Issue: i, PmPlanIssue: p))
            .ToList()
            .ForEach(x =>
            {
                x.Issue.PmPlan = x.PmPlanIssue.PmPlan;
                x.Issue.PmPlanSummary = x.PmPlanIssue.PmPlanSummary;
            });
    }

    private async Task RetrieveAllData()
    {
        // Get all PMPLAN tickets
        Console.WriteLine("Extracting PMPLAN tickets...");
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest AND status NOT IN (\"Feature delivered\", Cancelled) ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        this.pmPlans = (await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields)).Select(i => new PmPlanIssue(i)).ToList();

        // Get all children of each PMPLAN
        var childrenJql = "type IN (Story, Improvement, Bug, Epic, \"Table Definition\", \"Schema Task\") AND (issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        foreach (var pmPlan in this.pmPlans)
        {
            pmPlan.ChildrenStories = (await runner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.Key), Fields)).Select(i => new JiraIssue(i).AddPmPlanDetails(pmPlan)).ToList();
            Console.WriteLine($"Fetched {pmPlan.ChildrenStories.Count} children for {pmPlan.Key}");
            pmPlan.TotalStoryPoints = pmPlan.ChildrenStories.Sum(issue => issue.StoryPoints);
            pmPlan.RunningTotalWorkDone = pmPlan.TotalWorkDone = pmPlan.ChildrenStories.Where(issue => issue.Status == Constants.DoneStatus).Sum(issue => issue.StoryPoints);
        }

        // Get all tickets in open and future sprints for the teams.
        var query = """project = "JAVPM" AND "Team[Team]" IN (60412efa-7e2e-4285-bb4e-f329c3b6d417, 1a05d236-1562-4e58-ae88-1ffc6c5edb32) AND (Sprint IN openSprints() OR Sprint IN futureSprints())""";
        Console.WriteLine(query);
        this.allSprintTickets = (await runner.SearchJiraIssuesWithJqlAsync(query, Fields)).Select(i => new JiraIssue(i)).ToList();
    }

    private async Task UpdateSheetSprintMasterPlan()
    {
        var groupBySprint = this.allSprintTickets
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
                Tickets = g.Count(),
                SprintTickets = g.ToList()
            })
            .ToList();

        var sheetData = new List<IList<object?>>();
        var teamSprint = string.Empty;
        foreach (var row in groupBySprint.OrderBy(g => g.StartDate).ThenBy(g => g.Team).ThenBy(g => g.SprintName))
        {
            if (row.Team + row.SprintName != teamSprint)
            {
                // Sprint header row
                sheetData.Add(new List<object?>()); // empty row between sprints
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
            var pmPlanRecord = this.pmPlans.SingleOrDefault(p => p.Key == row.PmPlan);
            var doneSprintTickets = row.SprintTickets.Where(t => t.Status == Constants.DoneStatus).Sum(t => t.StoryPoints);
            // Dont count work just done during this sprint
            var runningTotalWorkDone = (pmPlanRecord?.RunningTotalWorkDone - doneSprintTickets) ?? 0.0;
            var ticketsWithNoEstimate = row.SprintTickets.Count(t => t.Status != Constants.DoneStatus && t.StoryPoints <= 0 && t.Type != Constants.EpicType);
            var percentCompleteStartOfSprint = runningTotalWorkDone / (pmPlanRecord?.TotalStoryPoints <= 0 ? 1 : pmPlanRecord?.TotalStoryPoints);
            var percentCompleteEndOfSprint = (runningTotalWorkDone + row.StoryPoints) / (pmPlanRecord?.TotalStoryPoints <= 0 ? 1 : pmPlanRecord?.TotalStoryPoints);
            var rowData = new List<object?>
            {
                null, //Team
                null, //Sprint
                null, //Start Date
                string.IsNullOrEmpty(row.PmPlan) ? "No PMPLAN" : row.PmPlan,
                row.Summary,
                row.Tickets, // Tickets in Sprint
                row.StoryPoints, // Story Points in Sprint
                pmPlanRecord?.TotalStoryPoints - runningTotalWorkDone, // Total Work remaining
                ticketsWithNoEstimate, // Count of tickets with no estimate
                percentCompleteStartOfSprint,
                percentCompleteEndOfSprint
            };
            sheetData.Add(rowData);
            teamSprint = row.Team + row.SprintName;

            if (pmPlanRecord is not null)
            {
                pmPlanRecord.RunningTotalWorkDone = runningTotalWorkDone + row.StoryPoints;
            }
        }

        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.ClearRange("Sprint-Master-Plan", "A2:Z10000");
        sheetUpdater.EditSheet("Sprint-Master-Plan!A2", sheetData);
        //await sheetUpdater.ApplyDateFormat("Sprints-PMPlans", 2, "d mmm yy");
    }

    private record PmPlanIssue
    {
        public PmPlanIssue(dynamic issue)
        {
            Key = JiraFields.Key.Parse(issue);
            Summary = JiraFields.Summary.Parse(issue);
        }

        public List<JiraIssue> ChildrenStories { get; set; } = new();

        public string Key { get; }
        public double RunningTotalWorkDone { get; set; }
        public string Summary { get; }
        public double TotalStoryPoints { get; set; }
        public double TotalWorkDone { get; set; }
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
        public string Type { get; }

        public JiraIssue AddPmPlanDetails(PmPlanIssue pmPlan)
        {
            PmPlan = pmPlan.Key;
            PmPlanSummary = pmPlan.Summary;
            return this;
        }
    }
}
