namespace BensEngineeringMetrics.Slack;

public record SlackMessage(string ChannelId, string UserId, string Message, DateTimeOffset LastMessageTimestamp, string Type, string SubType);
