using System.Text.Json;

namespace BensJiraConsole;

public class SlackClient : ISlackClient
{
    private const string BaseApiUrl = "https://slack.com/api/";

    public async Task<IReadOnlyList<SlackChannel>> FindAllChannels(string partialChannelName)
    {
        if (string.IsNullOrWhiteSpace(partialChannelName))
        {
            throw new ArgumentException("Channel name cannot be null or empty.", nameof(partialChannelName));
        }

        var (allChannels, totalChannels) = await GetAllSlackChannels(partialChannelName);

        Console.WriteLine($"Total channels retrieved and searched: {totalChannels}");
        Console.WriteLine($"Found {allChannels.Count} channel(s) matching '{partialChannelName}'");

        // Join channels and fetch last message timestamp for each channel
        var channelsWithTimestamps = new List<SlackChannel>();
        var skippedChannels = 0;
        foreach (var channel in allChannels)
        {
            // Try to join the channel first (if not already a member)
            if (!await JoinChannel(channel.Id, channel.IsPrivate))
            {
                skippedChannels++;
            }

            // Now try to get the last message timestamp
            var lastMessageTimestamp = await GetLastMessageTimestampAsync(channel.Id);
            channelsWithTimestamps.Add(channel with { LastMessageTimestamp = lastMessageTimestamp });
        }

        if (skippedChannels > 0)
        {
            Console.WriteLine($"Note: Could not retrieve timestamps for {skippedChannels} channel(s)");
        }

        return channelsWithTimestamps;
    }

    public async Task<bool> JoinChannel(string channelId, bool isPrivate)
    {
        // Private channels cannot be joined automatically - they require an invite
        if (isPrivate)
        {
            // For private channels, we can't auto-join, but we'll still try to access history
            // If the bot was previously invited, it will work
            return false;
        }

        try
        {
            var url = $"{BaseApiUrl}conversations.join?channel={Uri.EscapeDataString(channelId)}";

            var response = await App.HttpSlack.PostAsync(url, null);
            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseContent);

            if (jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) && okProperty.GetBoolean())
            {
                return true;
            }

            // Check if bot is already in channel (this is fine)
            if (jsonDocument.RootElement.TryGetProperty("error", out var errorProperty))
            {
                var error = errorProperty.GetString();
                if (error == "already_in_channel")
                {
                    return true; // Already a member, treat as success
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<SlackMessage>> GetMessages(string channelId, int limitToNumberOfMessages = 10)
    {
        JsonDocument jsonDocument;
        try
        {
            var url = $"{BaseApiUrl}conversations.history?channel={Uri.EscapeDataString(channelId)}&limit={limitToNumberOfMessages}";

            var response = await App.HttpSlack.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();
            jsonDocument = JsonDocument.Parse(responseContent);
        }
        catch (Exception ex)
        {
            // Any other errors - return null
            Console.WriteLine(ex);
            return new List<SlackMessage>();
        }

        if (!jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) || !okProperty.GetBoolean())
        {
            // Check for specific error types
            if (jsonDocument.RootElement.TryGetProperty("error", out var errorProperty))
            {
                var error = errorProperty.GetString();
                if (error is "not_in_channel" or "channel_not_found")
                {
                    // Bot is not a member of the channel - this is expected for some channels
                    return new List<SlackMessage>();
                }
            }

            // Other errors - return null
            return new List<SlackMessage>();
        }

        var messages = new List<SlackMessage>();
        if (jsonDocument.RootElement.TryGetProperty("messages", out var messagesProperty))
        {
            // Get the first (most recent) message
            foreach (var messageJson in messagesProperty.EnumerateArray())
            {
                var message = new SlackMessage(
                    channelId,
                    messageJson.GetProperty("user").GetString()!,
                    messageJson.GetProperty("text").GetString()!,
                    DateTimeOffset.FromUnixTimeSeconds((long)messageJson.GetProperty("ts").GetDouble()).ToLocalTime());
                messages.Add(message);
            }
        }

        return messages;
    }

    private static async Task<(List<SlackChannel> matchedChannels, int totalChannels)> GetAllSlackChannels(string partialChannelName)
    {
        var matchedChannels = new List<SlackChannel>();
        string? cursor = null;
        var totalChannels = 0;

        do
        {
            var url = $"{BaseApiUrl}conversations.list?types=public_channel&limit=1000&exclude_archived=true";
            if (!string.IsNullOrEmpty(cursor))
            {
                url += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var response = await App.HttpSlack.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseContent);

            if (!jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) || !okProperty.GetBoolean())
            {
                var error = jsonDocument.RootElement.TryGetProperty("error", out var errorProperty)
                    ? errorProperty.GetString()
                    : "Unknown error";
                throw new InvalidOperationException($"Slack API error: {error}");
            }

            if (jsonDocument.RootElement.TryGetProperty("channels", out var channelsProperty))
            {
                foreach (var channel in channelsProperty.EnumerateArray())
                {
                    totalChannels++;
                    if (channel.TryGetProperty("name", out var nameProperty))
                    {
                        var channelName = nameProperty.GetString();
                        if (!string.IsNullOrEmpty(channelName) && channelName.Contains(partialChannelName, StringComparison.OrdinalIgnoreCase))
                        {
                            var channelId = channel.TryGetProperty("id", out var idProperty) ? idProperty.GetString()! : "Unknown";
                            var isPrivate = channel.TryGetProperty("is_private", out var isPrivateProperty) && isPrivateProperty.GetBoolean();

                            matchedChannels.Add(new SlackChannel
                            (
                                channelId,
                                channelName,
                                isPrivate,
                                null // Will be populated after collecting all channels
                            ));
                            // Is Archived: channel.TryGetProperty("is_archived", out var isArchivedProperty) && isArchivedProperty.GetBoolean()
                        }
                    }
                }
            }

            // Check for next cursor in response_metadata
            cursor = null;
            if (jsonDocument.RootElement.TryGetProperty("response_metadata", out var responseMetadataProperty))
            {
                if (responseMetadataProperty.TryGetProperty("next_cursor", out var nextCursorProperty))
                {
                    var nextCursorValue = nextCursorProperty.GetString();
                    if (!string.IsNullOrEmpty(nextCursorValue))
                    {
                        cursor = nextCursorValue;
                    }
                }
            }
        } while (!string.IsNullOrEmpty(cursor));

        return (matchedChannels, totalChannels);
    }

    private async Task<DateTimeOffset?> GetLastMessageTimestampAsync(string channelId)
    {
        var messages = await GetMessages(channelId, 1);
        if (messages.Any())
        {
            return messages.First().LastMessageTimestamp;
        }

        return null;
    }
}
