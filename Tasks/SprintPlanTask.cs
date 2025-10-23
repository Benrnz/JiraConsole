namespace BensJiraConsole.Tasks;

public class SprintPlanTask(IJiraQueryRunner runner, ICsvExporter exporter, IWorkSheetUpdater sheetUpdater, ExportPmPlanStories pmPlanStoriesTask) : IJiraExportTask
{
    private const string GoogleSheetId = "1iS6iB3EA38SHJgDu8rpMFcouGlu1Az8cntKA52U07xU";
    private const string TaskKey = "SPRINT_PLAN";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.Priority,
        JiraFields.Team,
        JiraFields.StoryPoints,
        JiraFields.Sprint,
        JiraFields.SprintStartDate,
        JiraFields.IssueType,
        JiraFields.ParentKey
    ];

    private static readonly IFieldMapping[] PmPlanFields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.IssueType,
        JiraFields.PmPlanHighLevelEstimate,
        JiraFields.EstimationStatus,
        JiraFields.StoryPoints,
        JiraFields.IsReqdForGoLive
    ];

    public string Description => "Export to a Google Sheet a over-arching plan of all future sprints for the two project teams.";
    public string Key => TaskKey;

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        var issues = await ExtractAllSprintsAndTickets();

        await SprintPmPlan(issues);
    }

    private async Task<List<PmPlanIssue>> ExtractPmPlanTickets()
    {
        Console.WriteLine("Extracting PMPLAN tickets...");
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "(issue in (linkedIssues(\"{{0}}\")) OR parent in (linkedIssues(\"{{0}}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var pmPlans = (await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, [JiraFields.Summary, JiraFields.EstimationStatus, JiraFields.IsReqdForGoLive, JiraFields.PmPlanHighLevelEstimate]))
            .Select(CreatePmPlanIssue)
            .ToList();
        foreach (var pmPlan in pmPlans)
        {
            var childrenIssues = await runner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.Key), Fields);
            pmPlan.TotalStoryPoints = childrenIssues.Sum(issue => issue.StoryPoints);
            pmPlan.TotalStoryPointsRemaining = pmPlan.TotalStoryPoints - childrenIssues.Where(issue => issue.Status == Constants.DoneStatus).Sum(issue => issue.StoryPoints);
        }

        return pmPlans;
    }

    private PmPlanIssue CreatePmPlanIssue(dynamic i)
    {
        return new PmPlanIssue(
            JiraFields.Key.Parse(i),
            JiraFields.Summary.Parse(i),
            JiraFields.PmPlanHighLevelEstimate.Parse(i) ?? 0.0);
    }

    private async Task<IReadOnlyList<JiraIssueWithPmPlan>> RetrieveAllStoriesMappingToPmPlan()
    {
        var jqlPmPlans = "IssueType = Idea AND \"PM Customer[Checkboxes]\"= Envest ORDER BY Key";
        Console.WriteLine(jqlPmPlans);
        var childrenJql = "(issue in (linkedIssues(\"{0}\")) OR parent in (linkedIssues(\"{0}\"))) ORDER BY key";
        Console.WriteLine($"ForEach PMPLAN: {childrenJql}");
        var pmPlans = await runner.SearchJiraIssuesWithJqlAsync(jqlPmPlans, PmPlanFields);

        var allIssues = new Dictionary<string, JiraIssueWithPmPlan>(); // Ensure the final list of JAVPMs is unique NO DUPLICATES
        foreach (var pmPlan in pmPlans)
        {
            var children = await runner.SearchJiraIssuesWithJqlAsync(string.Format(childrenJql, pmPlan.key), Fields);
            Console.WriteLine($"Fetched {children.Count} children for {pmPlan.key}");
            foreach (var child in children)
            {
                JiraIssueWithPmPlan issue = CreateJiraIssueWithPmPlan(child, pmPlan);
                allIssues.TryAdd(issue.Key, issue);
            }
        }

        return allIssues.Values.ToList();
    }

    private JiraIssueWithPmPlan CreateJiraIssueWithPmPlan(dynamic i, dynamic pmPlan)
    {
        string key = JiraFields.Key.Parse(i);
        string sprintField = JiraFields.Sprint.Parse(i) ?? "No Sprint";
        var sprintDate = JiraFields.SprintStartDate.Parse(i);
        var teamField = JiraFields.Team.Parse(i) ?? "No Team";
        var storyPointsField = JiraFields.StoryPoints.Parse(i) ?? 0.0;

        var typedIssue = new JiraIssueWithPmPlan(
            pmPlan.key,
            key,
            JiraFields.Summary.Parse(i),
            teamField,
            sprintField,
            sprintDate,
            JiraFields.Status.Parse(i),
            JiraFields.IssueType.Parse(i),
            storyPointsField,
            JiraFields.IsReqdForGoLive.Parse(pmPlan),
            JiraFields.EstimationStatus.Parse(pmPlan),
            JiraFields.PmPlanHighLevelEstimate.Parse(pmPlan),
            JiraFields.Created.Parse(i),
            JiraFields.Summary.Parse(pmPlan),
            JiraFields.ParentKey.Parse(i));
        return typedIssue;
    }

    private async Task<List<JiraIssueWithPmPlan>> ExtractAllSprintsAndTickets()
    {
        var query = """project = "JAVPM" AND "Team[Team]" IN (60412efa-7e2e-4285-bb4e-f329c3b6d417, 1a05d236-1562-4e58-ae88-1ffc6c5edb32) AND (Sprint IN openSprints() OR Sprint IN futureSprints())""";
        Console.WriteLine(query);
        Console.WriteLine();

        // Get and group the data by Team and by Sprint.
        var sprintTickets = (await runner.SearchJiraIssuesWithJqlAsync(query, Fields)).Select(CreateJiraIssue).ToList();

        // Find PMPLAN for each issue if it exists.
        // Duplicate work
        var pmPlanStories = await pmPlanStoriesTask.RetrieveAllStoriesMappingToPmPlan();
        sprintTickets.Join(pmPlanStories, i => i.Key, p => p.Key, (i, p) => (Issue: i, PmPlan: p))
            .ToList()
            .ForEach(x =>
            {
                x.Issue.PmPlan = x.PmPlan.PmPlan;
                x.Issue.PmPlanSummary = x.PmPlan.PmPlanSummary;
            });

        sprintTickets = sprintTickets.OrderBy(i => i.Team)
            .ThenBy(i => i.SprintStartDate)
            .ThenBy(i => i.Sprint)
            .ThenBy(i => i.PmPlan)
            .ToList();

        // temp save to CSV
        exporter.SetFileNameMode(FileNameMode.Auto, Key + "_FullData");
        var file = exporter.Export(sprintTickets);

        // Export to Google Sheets.
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.CsvFilePathAndName = file;
        await sheetUpdater.ClearRange("Data");
        await sheetUpdater.ImportFile("'Data'!A1");
        return sprintTickets;
    }

    private async Task SprintPmPlan(List<JiraIssueWithPmPlan> issues)
    {
        var pmPlans = await ExtractPmPlanTickets();

        var groupBySprint = issues
            .GroupBy(i => (i.Team, i.SprintStartDate, i.Sprint, i.PmPlan, i.PmPlanSummary))
            .OrderBy(g => g.Key.Team)
            .ThenBy(g => g.Key.SprintStartDate)
            .ThenBy(g => g.Key.Sprint)
            .Select(g => new
            {
                g.Key.Team,
                StartDate = g.Key.SprintStartDate,
                SprintName = g.Key.Sprint,
                PMPLAN = g.Key.PmPlan,
                Summary = g.Key.PmPlanSummary,
                StoryPoints = g.Sum(x => x.StoryPoints),
                Tickets = g.Count()
            })
            .ToList();

        var sheetData = new List<IList<object?>>();
        var teamSprint = string.Empty;
        foreach (var row in groupBySprint.OrderBy(g => g.StartDate).ThenBy(g => g.Team).ThenBy(g => g.SprintName))
        {
            var pmPlanRecord = pmPlans.Single(p => p.Key == row.PMPLAN);
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
            var rowData = new List<object?>
            {
                null,
                null,
                null,
                string.IsNullOrEmpty(row.PMPLAN) ? "No PMPLAN" : row.PMPLAN,
                row.Summary,
                row.Tickets,
                row.StoryPoints,
                pmPlanRecord.TotalStoryPointsRemaining,
                (pmPlanRecord.TotalStoryPoints - pmPlanRecord.TotalStoryPointsRemaining) / pmPlanRecord.TotalStoryPoints <= 0 ? 1 : pmPlanRecord.TotalStoryPointsRemaining,
                (pmPlanRecord.TotalStoryPoints - pmPlanRecord.TotalStoryPointsRemaining + row.StoryPoints) / pmPlanRecord.TotalStoryPoints <= 0 ? 1 : pmPlanRecord.TotalStoryPointsRemaining
            };
            sheetData.Add(rowData);
            teamSprint = row.Team + row.SprintName;
        }

        await sheetUpdater.Open(GoogleSheetId);
        await sheetUpdater.ClearRange("Sprints-PMPlans", "A2:Z10000");
        await sheetUpdater.EditSheet("Sprints-PMPlans!A2", sheetData);
        //await sheetUpdater.ApplyDateFormat("Sprints-PMPlans", 2, "d mmm yy");
    }

    private record PmPlanIssue(string Key, string Summary, double EstimatedStoryPoints)
    {
        public double TotalStoryPoints { get; set; }

        public double TotalStoryPointsRemaining { get; set; }
    }

    public record JiraIssueWithPmPlan(
        string PmPlan,
        string Key,
        string Summary,
        string Team,
        string Sprint,
        DateTimeOffset SprintStartDate,
        string Status,
        string Type,
        double StoryPoints,
        bool IsReqdForGoLive,
        string? EstimationStatus,
        double PmPlanHighLevelEstimate,
        DateTimeOffset CreatedDateTime,
        string PmPlanSummary,
        string? ParentEpic = null);
}
