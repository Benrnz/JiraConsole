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
                            allChannels.Add(new SlackChannel
                            (
                                channel.TryGetProperty("id", out var idProperty) ? idProperty.GetString()! : "Unknown", channelName,
                                channel.TryGetProperty("is_private", out var isPrivateProperty) && isPrivateProperty.GetBoolean()
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
        return allChannels;
    }
}
