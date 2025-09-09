namespace BensJiraConsole.Tasks;

public class CalculateDailyReportTask : IJiraExportTask
{
    private const string GoogleSheetId = "1PCZ6APxgEF4WDJaMqLvXDztM47VILEy2RdGDgYiXguQ";

    private static readonly FieldMapping[] Fields =
    [
        JiraFields.Summary,
        JiraFields.Status,
        JiraFields.StoryPoints,
        JiraFields.Team,
        JiraFields.Sprint,
        JiraFields.AssigneeDisplay,
        JiraFields.FlagCount
    ];

    private readonly GoogleSheetReader reader = new(GoogleSheetId);

    public string Key => "DAILY";
    public string Description => "Calculate the _daily_ stats for the daily report for the two teams involved.";

    public async Task ExecuteAsync(string[] args)
    {
        Console.WriteLine(Description);
        DateTime sprintStart;
        do
        {
            Console.Write("Enter the start date of the sprint (dd-MM-yyyy):");
        } while (!DateTime.TryParse(Console.ReadLine(), out sprintStart));

        var runner = new JiraQueryDynamicRunner();

        // Superclass team
        var jql = """Project = JAVPM AND "Team[Team]" = 1a05d236-1562-4e58-ae88-1ffc6c5edb32 AND Sprint IN openSprints()""";
        await CalculateTeamStats(runner, jql, "Superclass", sprintStart);

        // Ruby Ducks team
        jql = """Project = JAVPM AND "Team[Team]" = 60412efa-7e2e-4285-bb4e-f329c3b6d417 AND Sprint IN openSprints()""";
        await CalculateTeamStats(runner, jql, "Ruby Ducks", sprintStart);
    }

    private async Task CalculateTeamStats(JiraQueryDynamicRunner runner, string jql, string teamName, DateTime sprintStart)
    {
        Console.WriteLine($"Calculating team stats for {teamName}");
        Console.WriteLine(jql);
        var tickets = (await runner.SearchJiraIssuesWithJqlAsync(jql, Fields)).Select(CreateJiraIssue).ToList();
        var totalTickets = tickets.Count();
        var totalStoryPoints = tickets.Sum(t => t.StoryPoints);
        var remainingTickets = tickets.Count(t => t.Status != Constants.DoneStatus);
        var remainingStoryPoints = tickets.Where(t => t.Status != Constants.DoneStatus).Sum(t => t.StoryPoints);
        var ticketsInQa = tickets.Count(t => t.Status == Constants.InQaStatus);
        var ticketsInDev = tickets.Count(t => t.Status == Constants.InDevStatus);
        var ticketsFlagged = tickets.Sum(t => t.FlagCount);
        Console.WriteLine($"{teamName} Team Stats:");
        Console.WriteLine($"     - Total Tickets: {totalTickets}, {remainingTickets} remaining ({1 - ((double)remainingTickets / totalTickets):P0} Done). ");
        Console.WriteLine($"     - Total Story Points: {totalStoryPoints}, {remainingStoryPoints} remaining ({1 - (remainingStoryPoints / totalStoryPoints):P0} Done).");
        Console.WriteLine($"     - In Dev: {ticketsInDev}, In QA: {ticketsInQa}");
        Console.WriteLine($"     - Number of Flags raised: {ticketsFlagged}");

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
            JiraFields.Key.Parse<string>(ticket),
            JiraFields.Status.Parse<string>(ticket),
            JiraFields.StoryPoints.Parse<double>(ticket) ?? 0,
            JiraFields.Team.Parse<string>(ticket),
            JiraFields.AssigneeDisplay.Parse<string?>(ticket),
            JiraFields.FlagCount.Parse<int>(ticket)
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
            int.Parse(sheetData[5].ToString() ?? "0")
        );
    }

    private async Task ProcessNormalSprintDay(string teamName, DateTime sprintStart, List<JiraIssue> tickets)
    {
        var sheetData = await this.reader.ReadData($"'{teamName}'!A1:G1000");
        var headerRow = sheetData.FirstOrDefault();
        if (headerRow is null || headerRow.Count < 7 || !DateTime.TryParse(headerRow[6].ToString(), out var sheetStart))
        {
            Console.WriteLine("Sheet appears blank or invalid, assuming start of sprint is today...");
            await ProcessStartOfSprint(teamName, sprintStart, tickets);
            return;
        }

        if (sheetStart != sprintStart)
        {
            Console.WriteLine($"You have entered a start date for the sprint of {sprintStart:d} but this doesn't match the date in the sheet of {sheetStart:d}.  Please check and try again.");
            return;
        }

        var originalTickets = sheetData.Skip(1).Select(CreateJiraIssue).ToList(); // skip header row

        Console.WriteLine("Removed tickets since start of sprint:");
        var removedTickets = tickets.Where(t => originalTickets.All(o => o.Key != t.Key)).ToList();
        if (removedTickets.Any())
        {
            Console.Write("    ");
            removedTickets.ForEach(t => Console.Write($"{t.Key}, "));
            Console.WriteLine();
            Console.WriteLine($"    {removedTickets.Count} total.");
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
            newTickets.ForEach(t => Console.Write($"{t.Key}, "));
            Console.WriteLine();
            Console.WriteLine($"    {newTickets.Count} total.");
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
        var exporter = new SimpleCsvExporter(Key)
        {
            Mode = SimpleCsvExporter.FileNameMode.ExactName,
            OverrideSerialiseHeader = () => $"Key,Status,StoryPoints,Team,Assignee,FlagCount,{sprintStart:yyyy-MM-dd}"
        };
        var pathAndFileName = exporter.Export(tickets, fileName);
        var updater = new GoogleSheetUpdater(pathAndFileName, GoogleSheetId);
        await updater.DeleteSheet($"{teamName}");
        await updater.AddSheet($"{teamName}");
        await updater.EditGoogleSheet($"'{teamName}'!A1");
        Console.WriteLine("Successfully recorded the list of tickets brought into the beginning of the sprint.");
    }

    private record JiraIssue(string Key, string Status, double StoryPoints, string Team, string? Assignee, int FlagCount);
}
