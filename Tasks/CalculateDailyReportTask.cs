namespace BensJiraConsole.Tasks;

public class CalculateDailyReportTask(ICsvExporter exporter, IJiraQueryRunner runner, IWorkSheetReader sheetReader, IWorkSheetUpdater sheetUpdater) : IJiraExportTask
{
    private const string GoogleSheetId = "1PCZ6APxgEF4WDJaMqLvXDztM47VILEy2RdGDgYiXguQ";
    private const string KeyString = "DAILY";

    private static readonly IFieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.StoryPoints,
        JiraFields.Team,
        JiraFields.Sprint,
        JiraFields.AssigneeDisplay,
        JiraFields.FlagCount,
        JiraFields.IssueType,
        JiraFields.Severity,
        JiraFields.BugType
    ];

    public string Key => KeyString;
    public string Description => "Calculate the _daily_ stats for the daily report for the two teams involved.";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        await sheetReader.Open(GoogleSheetId);
        var sprintStart = args.Length > 1 ? DateTime.Parse(args[1]) : DateTime.MinValue;
        SuggestTwoMostRecentMondays(sprintStart);
        while (sprintStart == DateTime.MinValue)
        {
            Console.WriteLine();
            Console.Write("Enter the start date of the sprint (dd-MM-yyyy):");
            DateTime.TryParse(Console.ReadLine(), out sprintStart);
        }

        // Superclass team
        var jql = $"""Project = JAVPM AND "Team[Team]" = {Constants.TeamSuperclass} AND Sprint IN openSprints()""";
        await CalculateTeamStats(jql, "Superclass", sprintStart);

        // Ruby Ducks team
        jql = $"""Project = JAVPM AND "Team[Team]" = {Constants.TeamRubyDucks} AND Sprint IN openSprints()""";
        await CalculateTeamStats(jql, "Ruby Ducks", sprintStart);

        // Spearhead team
        jql = $"""Project = JAVPM AND "Team[Team]" = {Constants.TeamSpearhead} AND Sprint IN openSprints()""";
        await CalculateTeamStats(jql, "Spearhead", sprintStart);

        // Officetech team
        jql = """Project = OTPM AND Sprint IN openSprints()""";
        await CalculateTeamStats(jql, "Officetech", sprintStart);

        Console.WriteLine("---------------------------------------------------------------------------------------------------");
        Console.WriteLine();
    }

    private async Task CalculateTeamStats(string jql, string teamName, DateTime sprintStart)
    {
        Console.WriteLine();
        Console.WriteLine("---------------------------------------------------------------------------------------------------");
        Console.WriteLine($"Calculating team stats for {teamName}");
        var tickets = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(CreateJiraIssue).ToList();
        var totalTickets = tickets.Count();
        var totalStoryPoints = tickets.Sum(t => t.StoryPoints);
        var remainingTickets = tickets.Count(t => t.Status != Constants.DoneStatus);
        var remainingStoryPoints = tickets.Where(t => t.Status != Constants.DoneStatus).Sum(t => t.StoryPoints);
        var ticketsInQa = tickets.Count(t => t.Status == Constants.InQaStatus);
        var ticketsInDev = tickets.Count(t => t.Status == Constants.InDevStatus);
        var ticketsFlagged = tickets.Sum(t => t.FlagCount);
        var p1Bugs = tickets.Count(t => t.Type == Constants.BugType && t is { Severity: Constants.SeverityCritical, BugType: Constants.BugTypeProduction or Constants.BugTypeUat });
        var p2Bugs = tickets.Count(t => t.Type == Constants.BugType && t is { Severity: Constants.SeverityMajor, BugType: Constants.BugTypeProduction or Constants.BugTypeUat });
        Console.WriteLine($"{teamName} Team Stats:");
        Console.WriteLine($"     - Total Tickets: {totalTickets}, {remainingTickets} remaining, {totalTickets - remainingTickets} done. ({1 - ((double)remainingTickets / totalTickets):P0} Done). ");
        Console.WriteLine(
            $"     - Total Story Points: {totalStoryPoints}, {remainingStoryPoints} remaining, {totalStoryPoints - remainingStoryPoints:F1} done. ({1 - (remainingStoryPoints / totalStoryPoints):P0} Done).");
        Console.WriteLine($"     - In Dev: {ticketsInDev}, In QA: {ticketsInQa}");
        Console.WriteLine($"     - Number of Flags raised: {ticketsFlagged}");
        if (p1Bugs > 0 || p2Bugs > 0)
        {
            Console.WriteLine($"     - *** P1 Bugs: {p1Bugs}, P2 Bugs: {p2Bugs} ***");
        }

        if (sprintStart == DateTime.Today)
        {
            await ProcessStartOfSprint(teamName, sprintStart, tickets);
        }
        else
        {
            await ProcessNormalSprintDay(teamName, sprintStart, tickets);
        }
    }

    private JiraIssue CreateJiraIssue(dynamic ticket)
    {
        return new JiraIssue(
            JiraFields.Key.Parse(ticket),
            JiraFields.Status.Parse(ticket),
            JiraFields.StoryPoints.Parse(ticket) ?? 0,
            JiraFields.Team.Parse(ticket),
            JiraFields.AssigneeDisplay.Parse(ticket),
            JiraFields.FlagCount.Parse(ticket),
            JiraFields.IssueType.Parse(ticket),
            JiraFields.Severity.Parse(ticket) ?? string.Empty,
            JiraFields.BugType.Parse(ticket) ?? string.Empty
        );
    }

    private JiraIssue CreateJiraIssue(List<object> sheetData)
    {
        return new JiraIssue(
            sheetData[0].ToString() ?? throw new NotSupportedException("Key"),
            sheetData[1].ToString() ?? throw new NotSupportedException("Status"),
            double.Parse(sheetData[2].ToString() ?? "0"),
            sheetData[3].ToString() ?? throw new NotSupportedException("Team"),
            sheetData[4].ToString() ?? string.Empty,
            int.Parse(sheetData[5].ToString() ?? "0"),
            string.Empty,
            string.Empty
        );
    }

    private async Task ProcessNormalSprintDay(string teamName, DateTime sprintStart, List<JiraIssue> tickets)
    {
        var sheetData = await sheetReader.ReadData($"'{teamName}'!A1:G1000");
        var headerRow = sheetData.FirstOrDefault();
        if (headerRow is null || headerRow.Count < 7 || !DateTime.TryParse(headerRow[6].ToString(), out var sheetStart))
        {
            Console.WriteLine("Sheet appears blank or invalid, assuming start of sprint is today...");
            await ProcessStartOfSprint(teamName, sprintStart, tickets);
            return;
        }

        if (sheetStart != sprintStart)
        {
            Console.WriteLine($"You have entered a start date for the sprint of {sprintStart:d} but this doesn't match the date in the sheet of {sheetStart:d}.");
            Console.WriteLine("Assuming start of sprint is the date provided...");
            await ProcessStartOfSprint(teamName, sprintStart, tickets);
            return;
        }

        var originalTickets = sheetData.Skip(1).Select(CreateJiraIssue).ToList(); // skip header row

        Console.WriteLine("Removed tickets since start of sprint:");
        var removedTickets = tickets.Where(t => originalTickets.All(o => o.Key != t.Key)).ToList();
        if (removedTickets.Any())
        {
            Console.Write("    ");
            removedTickets.ForEach(t => Console.Write($"{t.Key} ({t.StoryPoints}sp), "));
            Console.WriteLine();
            Console.WriteLine($"    {removedTickets.Count} total. {removedTickets.Sum(t => t.StoryPoints):F1}sp total.");
        }
        else
        {
            Console.WriteLine("    None");
        }

        Console.WriteLine("New tickets added since start of sprint:");
        var newTickets = originalTickets.Where(o => tickets.All(t => t.Key != o.Key)).ToList();
        if (newTickets.Any())
        {
            Console.Write("    ");
            newTickets.ForEach(t => Console.Write($"{t.Key} ({t.StoryPoints}sp), "));
            Console.WriteLine();
            Console.WriteLine($"    {newTickets.Count} total. {newTickets.Sum(t => t.StoryPoints):F1}sp total.");
        }
        else
        {
            Console.WriteLine("    None");
        }
    }

    private async Task ProcessStartOfSprint(string teamName, DateTime sprintStart, List<JiraIssue> tickets)
    {
        // Save the list of tickets to Google Drive
        Console.WriteLine("Today is the start of the new sprint.  Recording the list of tickets to Google Drive...");
        var fileName = $"{Key}_{teamName}";
        exporter.SetFileNameMode(FileNameMode.ExactName, fileName);
        var pathAndFileName = exporter.Export(tickets, () => $"Key,Status,StoryPoints,Team,Assignee,FlagCount,{sprintStart:yyyy-MM-dd}");
        await sheetUpdater.Open(GoogleSheetId);
        sheetUpdater.DeleteSheet($"{teamName}");
        sheetUpdater.AddSheet($"{teamName}");
        await sheetUpdater.ImportFile($"'{teamName}'!A1", pathAndFileName);
        await sheetUpdater.SubmitBatch();
        Console.WriteLine("Successfully recorded the list of tickets brought into the beginning of the sprint.");
    }

    private static void SuggestTwoMostRecentMondays(DateTime sprintStart)
    {
        if (sprintStart == DateTime.MinValue)
        {
            var today = DateTime.Today;
            var daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var mostRecentMonday = today.AddDays(-daysSinceMonday);
            var previousMonday = mostRecentMonday.AddDays(-7);
            Console.WriteLine($"Suggested start dates: {mostRecentMonday:dd-MM-yyyy} (most recent Monday) or {previousMonday:dd-MM-yyyy} (previous Monday).");
        }
    }

    private record JiraIssue(
        string Key,
        string Status,
        double StoryPoints,
        string Team,
        string? Assignee,
        int FlagCount,
        string Type,
        string? Severity = "",
        string? BugType = "");
}
