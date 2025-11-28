using System.Diagnostics.CodeAnalysis;
using BensJiraConsole.Jira;

namespace BensJiraConsole.Tasks;

public class TeamVelocityCalculator(IJiraQueryRunner runner, ICsvExporter exporter, IGreenHopperClient greenHopperClient)
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public async Task<(List<(string, int, double, int, double, int, double)> teamData, double totalStoryPointsAllTeams)> TeamVelocityTableGetTeamData(string project)
    {
        var teamData = new List<(string, int, double, int, double, int, double)>();
        var totalStoryPointsAllTeams = 0.0;
        foreach (var team in JiraConfig.Teams.Where(t => t.JiraProject == project))
        {
            var last5Sprints = (await runner.GetAllSprints(team.BoardId))
                .Where(t => t.State == Constants.SprintStateClosed)
                .OrderByDescending(t => t.StartDate)
                .Take(5);
            exporter.SetFileNameMode(FileNameMode.ExactName, $"Velocity-Last5Sprints-{team.TeamName}");
            exporter.Export(last5Sprints);

            var totalP1Count = 0;
            var totalP2Count = 0;
            var totalOtherCount = 0;
            var totalStoryPoints = 0.0;
            var totalP1StoryPoints = 0.0;
            var totalP2StoryPoints = 0.0;
            var totalOtherStoryPoints = 0.0;
            foreach (var sprint in last5Sprints)
            {
                var tickets = GetSprintTickets(team.BoardId, sprint.Id);

                // var tickets = (await runner.SearchJiraIssuesWithJqlAsync(
                //         $"sprint = {sprint.Id} AND status = Done", // this doesnt work because its the status as at now, not at sprint close time
                //         [JiraFields.Severity, JiraFields.IssueType, JiraFields.StoryPoints, JiraFields.BugType]))
                //     .Select(JiraIssue.CreateJiraIssueSlim)
                //     .ToList();

                exporter.SetFileNameMode(FileNameMode.ExactName, $"Velocity-TeamTickets-{team.TeamName}-{sprint.Id}");
                exporter.Export(tickets);
                // How many P1s did the team close in this sprint?
                var p1s = tickets.Where(t => t is { IssueType: Constants.BugType, BugType: Constants.BugTypeProduction })
                    .Count(t => t.Severity == Constants.SeverityCritical);
                totalP1Count += p1s;
                var p1StoryPoints = tickets.Where(t => t is { IssueType: Constants.BugType, BugType: Constants.BugTypeProduction, Severity: Constants.SeverityCritical })
                    .Sum(t => t.StoryPoints);
                totalP1StoryPoints += p1StoryPoints;
                // How many P2s did the team close in this sprint?
                var p2s = tickets.Where(t => t is { IssueType: Constants.BugType, BugType: Constants.BugTypeProduction })
                    .Count(t => t.Severity == Constants.SeverityMajor);
                totalP2Count += p2s;
                var p2StoryPoints = tickets.Where(t => t is { IssueType: Constants.BugType, BugType: Constants.BugTypeProduction, Severity: Constants.SeverityMajor })
                    .Sum(t => t.StoryPoints);
                totalP2StoryPoints += p2StoryPoints;
                // How many other bugs did the team close in this sprint?
                totalOtherCount += tickets.Count(t => t is { IssueType: Constants.BugType }) - p1s - p2s;
                // How many story points were *completed* in this sprint?
                totalStoryPoints += tickets.Sum(t => t.StoryPoints);
                totalOtherStoryPoints += tickets.Where(t => t.IssueType == Constants.BugType).Sum(t => t.StoryPoints) - p1StoryPoints - p2StoryPoints;
            }

            teamData.Add((
                team.TeamName,
                totalP1Count / 5,
                Math.Round(totalP1StoryPoints / totalStoryPoints, 2),
                totalP2Count / 5,
                Math.Round(totalP2StoryPoints / totalStoryPoints, 2),
                totalOtherCount / 5,
                Math.Round(totalOtherStoryPoints / totalStoryPoints, 2)));

            totalStoryPointsAllTeams += totalStoryPoints;
        }

        return (teamData, totalStoryPointsAllTeams);
    }

    private async Task<IEnumerable<JiraIssue>> GetSprintTickets(int boardId, int sprintId)
    {
        var sprintTickets = await greenHopperClient.GetSprintTicketsAsync(boardId, sprintId);

    }

    private record JiraIssue(string Key, string Severity, double StoryPoints, string IssueType, string BugType)
    {
        public static JiraIssue CreateJiraIssueSlim(dynamic d)
        {
            return new JiraIssue(
                JiraFields.Key.Parse(d),
                JiraFields.Severity.Parse(d) ?? string.Empty,
                JiraFields.StoryPoints.Parse(d) ?? 1.0,
                JiraFields.IssueType.Parse(d) ?? string.Empty,
                JiraFields.BugType.Parse(d) ?? string.Empty);
        }
    }
}
