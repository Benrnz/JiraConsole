namespace BensJiraConsole;

public interface ISlackClient
{
    Task<IReadOnlyList<SlackChannel>> FindAllChannels(string partialChannelName);

    Task<IReadOnlyList<SlackMessage>> GetMessages(string channelId, int limitToNumberOfMessages = 10);

    /// <summary>
    /// Join the specified Slack channel. This can be called safely even if the bot is already a member.
    /// </summary>
    Task<bool> JoinChannel(string channelId, bool isPrivate);
}

public record SlackChannel(string Id, string Name, bool IsPrivate, DateTimeOffset? LastMessageTimestamp);

public record SlackMessage(string ChannelId, string UserId, string Message, DateTimeOffset LastMessageTimestamp);
