namespace BensJiraConsole;

public interface ISlackClient
{
    Task<IReadOnlyList<SlackChannel>> FindAllChannels(string partialChannelName);
}

public record SlackChannel(string Id, string Name, bool IsPrivate, DateTimeOffset? LastMessageTimestamp);
