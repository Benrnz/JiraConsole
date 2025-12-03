namespace BensEngineeringMetrics.Jira;

public record TeamConfig(string TeamName, string TeamId, int BoardId, double MaxCapacity, string JiraProject);

public static class JiraConfig
{
    public static readonly TeamConfig[] Teams =
    [
        new("Superclass", Constants.TeamSuperclass, 419, 60, Constants.JavPmJiraProjectKey),
        new("RubyDucks", Constants.TeamRubyDucks, 420, 60, Constants.JavPmJiraProjectKey),
        new("Spearhead", Constants.TeamSpearhead, 418, 60, Constants.JavPmJiraProjectKey),
        new("Officetech", Constants.TeamOfficetech, 483, 35, Constants.OtPmJiraProjectKey),
        new("Integration", Constants.TeamIntegration, 450, 40, Constants.JavPmJiraProjectKey)
    ];
}
