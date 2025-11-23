namespace BensJiraConsole;

public interface ISlackClient
{
    Task<IReadOnlyList<SlackChannel>> FindAllChannels(string partialChannelName);
    Task<bool> JoinChannel(string channelId, bool isPrivate);
}

public record SlackChannel(string Id, string Name, bool IsPrivate, DateTimeOffset? LastMessageTimestamp);
