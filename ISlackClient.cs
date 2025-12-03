using BensEngineeringMetrics.Slack;

namespace BensEngineeringMetrics;

public interface ISlackClient
{
    Task<IReadOnlyList<SlackChannel>> FindAllChannels(string partialChannelName);

    Task<IReadOnlyList<SlackMessage>> GetMessages(string channelId, int limitToNumberOfMessages = 10);

    /// <summary>
    ///     Join the specified Slack channel. This can be called safely even if the bot is already a member.
    /// </summary>
    Task<bool> JoinChannel(string channelId, bool isPrivate);
}
