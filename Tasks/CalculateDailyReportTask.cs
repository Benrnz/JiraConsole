namespace BensJiraConsole.Tasks;

public class CalculateDailyReportTask : IJiraExportTask
{
    public string Key => "DAILY";
    public string Description => "Calculate the daily stats for the daily report for the two teams involved.";

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

    public async Task ExecuteAsync(string[] fields)
    {
        Console.WriteLine(Description);
        var runner = new JiraQueryDynamicRunner();

        // Superclass team
        var jql = """Project = JAVPM AND "Team[Team]" = 1a05d236-1562-4e58-ae88-1ffc6c5edb32 AND Sprint IN openSprints()""";
        await CalculateTeamStats(runner, jql, "Superclass");

        // Ruby Ducks team
        jql = """Project = JAVPM AND "Team[Team]" = 60412efa-7e2e-4285-bb4e-f329c3b6d417 AND Sprint IN openSprints()""";
        await CalculateTeamStats(runner, jql, "Ruby Ducks");

        // TODO 1: accept a date paramter to state the start of the sprint. If start of sprint is today, export the list of tickets to Google Drive.
        // TODO 2: If today is not start of sprint, compare todays list to Google Drive start of sprint list and report any differences.
    }

    private async Task CalculateTeamStats(JiraQueryDynamicRunner runner, string jql, string teamName)
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
        Console.WriteLine($"     - Total Tickets: {totalTickets}, {remainingTickets} remaining ({1 - (remainingStoryPoints / totalTickets):P0}). ");
        Console.WriteLine($"     - Total Story Points: {totalStoryPoints}, remaining ({remainingStoryPoints} {1 - (remainingStoryPoints / totalStoryPoints):P0}).");
        Console.WriteLine($"     - In Dev: {ticketsInDev}, In QA: {ticketsInQa}");
        Console.WriteLine($"     - Flagged Tickets: {ticketsFlagged}");
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

    private record JiraIssue(string Key, string Status, double StoryPoints, string Team, string? Assignee, int FlagCount);
}
