namespace BensEngineeringMetrics.Slack;

public record SlackChannel(string Id, string Name, bool IsPrivate, DateTimeOffset? LastMessageTimestamp);
