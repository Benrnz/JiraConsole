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

        var allChannels = new List<SlackChannel>();
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

                            allChannels.Add(new SlackChannel
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

        Console.WriteLine($"Total channels retrieved and searched: {totalChannels}");
        Console.WriteLine($"Found {allChannels.Count} channel(s) matching '{partialChannelName}'");

        // Fetch last message timestamp for each channel
        Console.WriteLine("Fetching last message timestamps...");
        var channelsWithTimestamps = new List<SlackChannel>();
        foreach (var channel in allChannels)
        {
            var lastMessageTimestamp = await GetLastMessageTimestampAsync(channel.Id);
            channelsWithTimestamps.Add(channel with {LastMessageTimestamp = lastMessageTimestamp});
        }

        return channelsWithTimestamps;
    }

    private async Task<DateTimeOffset?> GetLastMessageTimestampAsync(string channelId)
    {
        try
        {
            var url = $"{BaseApiUrl}conversations.history?channel={Uri.EscapeDataString(channelId)}&limit=1";

            var response = await App.HttpSlack.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDocument = JsonDocument.Parse(responseContent);

            if (!jsonDocument.RootElement.TryGetProperty("ok", out var okProperty) || !okProperty.GetBoolean())
            {
                // If there's an error (e.g., channel has no messages or access denied), return null
                return null;
            }

            if (jsonDocument.RootElement.TryGetProperty("messages", out var messagesProperty))
            {
                // Get the first (most recent) message
                foreach (var message in messagesProperty.EnumerateArray())
                {
                    if (message.TryGetProperty("ts", out var tsProperty))
                    {
                        var tsString = tsProperty.GetString();
                        if (!string.IsNullOrEmpty(tsString) && double.TryParse(tsString, out var timestamp))
                        {
                            // Convert Unix timestamp (seconds) to DateTimeOffset
                            return DateTimeOffset.FromUnixTimeSeconds((long)timestamp);
                        }
                    }
                    // Only process the first message since limit=1
                    break;
                }
            }

            return null;
        }
        catch
        {
            // If any error occurs, return null (channel might not have messages or access issues)
            return null;
        }
    }
}
