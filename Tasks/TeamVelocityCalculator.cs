using BensEngineeringMetrics.Jira;

// ReSharper disable InconsistentNaming

namespace BensEngineeringMetrics.Tasks;

public record TeamSprintMetrics(
    string TeamName,
    double AvgP1sClosed,
    double P1StoryPointRatio,
    double AvgP2sClosed,
    double P2StoryPointRatio,
    double AvgOtherBugsClosed,
    double OtherBugStoryPointRatio,
    double TotalStoryPointsClosed);

public class TeamVelocityCalculator(IJiraQueryRunner runner, IGreenHopperClient greenHopperClient)
{
    public async Task<List<TeamSprintMetrics>> TeamVelocityTableGetTeamData(string project)
    {
        var teamData = new List<TeamSprintMetrics>();
        var totalStoryPointsAllTeams = new double[4];

        foreach (var team in JiraConfig.Teams.Where(t => t.JiraProject == project))
        {
            var last5Sprints = (await runner.GetAllSprints(team.BoardId))
                .Where(t => t.State == Constants.SprintStateClosed)
                .OrderByDescending(t => t.StartDate)
                .Take(5);

            var teamP1Count = 0.0;
            var teamP2Count = 0.0;
            var teamOtherCount = 0.0;
            var teamStoryPoints = 0.0;
            var teamP1StoryPoints = 0.0;
            var teamP2StoryPoints = 0.0;
            var teamOtherStoryPoints = 0.0;
            foreach (var sprint in last5Sprints)
            {
                var tickets = await GetSprintTickets(team.BoardId, sprint.Id);

                // How many P1s did the team close in this sprint?
                var p1s = tickets.Count(t => t is { IssueType: Constants.BugType, Severity: Constants.SeverityCritical });
                teamP1Count += p1s;
                var p1StoryPoints = tickets.Where(t => t is { IssueType: Constants.BugType, Severity: Constants.SeverityCritical })
                    .Sum(t => t.StoryPoints);
                teamP1StoryPoints += p1StoryPoints;
                // How many P2s did the team close in this sprint?
                var p2s = tickets.Count(t => t is { IssueType: Constants.BugType, Severity: Constants.SeverityMajor });
                teamP2Count += p2s;
                var p2StoryPoints = tickets.Where(t => t is { IssueType: Constants.BugType, Severity: Constants.SeverityMajor })
                    .Sum(t => t.StoryPoints);
                teamP2StoryPoints += p2StoryPoints;
                // How many other bugs did the team close in this sprint?
                teamOtherCount += tickets.Count(t => t is { IssueType: Constants.BugType }) - p1s - p2s;
                // How many story points were *completed* in this sprint?
                teamOtherStoryPoints += tickets.Where(t => t.IssueType == Constants.BugType).Sum(t => t.StoryPoints) - p1StoryPoints - p2StoryPoints;
                teamStoryPoints += tickets.Sum(t => t.StoryPoints);
            }

            teamData.Add(new TeamSprintMetrics(
                team.TeamName,
                teamP1Count / 5,
                Math.Round(teamP1StoryPoints / teamStoryPoints, 2),
                teamP2Count / 5,
                Math.Round(teamP2StoryPoints / teamStoryPoints, 2),
                teamOtherCount / 5,
                Math.Round(teamOtherStoryPoints / teamStoryPoints, 2),
                teamStoryPoints));

            totalStoryPointsAllTeams[0] += teamStoryPoints;
            totalStoryPointsAllTeams[1] += teamP1StoryPoints;
            totalStoryPointsAllTeams[2] += teamP2StoryPoints;
            totalStoryPointsAllTeams[3] += teamOtherStoryPoints;
        }

        teamData.Insert(0, new TeamSprintMetrics(
            "Avg across all teams",
            teamData.Sum(t => t.AvgP1sClosed),
            Math.Round(totalStoryPointsAllTeams[1] / totalStoryPointsAllTeams[0], 1),
            teamData.Sum(t => t.AvgP2sClosed),
            Math.Round(totalStoryPointsAllTeams[2] / totalStoryPointsAllTeams[0], 1),
            teamData.Sum(t => t.AvgOtherBugsClosed),
            Math.Round(totalStoryPointsAllTeams[3] / totalStoryPointsAllTeams[0], 1),
            totalStoryPointsAllTeams[0]));

        return teamData;
    }

    private async Task<IReadOnlyList<JiraIssue>> GetSprintTickets(int boardId, int sprintId)
    {
        var sprintTickets = await greenHopperClient.GetSprintTicketsAsync(boardId, sprintId);

        // Filter to only completed tickets (those that were completed during the sprint)
        var completedTicketKeys = sprintTickets
            .Where(t => t.Status == Constants.DoneStatus)
            .Select(t => t.Key)
            .ToList();

        if (completedTicketKeys.Count == 0)
        {
            return [];
        }

        // Build JQL query to get full ticket details for completed tickets
        var keysQuery = string.Join(", ", completedTicketKeys.Select(k => $"\"{k}\""));
        var jql = $"key IN ({keysQuery})";

        // Query runner to get full ticket details with required fields
        var tickets = (await runner.SearchJiraIssuesWithJqlAsync(
                jql,
                [JiraFields.Severity, JiraFields.StoryPoints, JiraFields.IssueType, JiraFields.BugType]))
            .Select(JiraIssue.CreateJiraIssueSlim)
            .ToList();

        return tickets;
    }

    private record JiraIssue(string Key, string Severity, double StoryPoints, string IssueType)
    {
        public static JiraIssue CreateJiraIssueSlim(dynamic d)
        {
            return new JiraIssue(
                JiraFields.Key.Parse(d),
                JiraFields.Severity.Parse(d) ?? string.Empty,
                JiraFields.StoryPoints.Parse(d) ?? 1.0,
                JiraFields.IssueType.Parse(d) ?? string.Empty);
        }
    }
}
