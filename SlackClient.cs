// using System.Net.Http.Headers;
// using System.Text.Json;
//
// public class SlackClient
// {
//     public class Program
//     {
//         private const string SlackBotToken = "xoxb-YOUR-SLACK-BOT-TOKEN";
//
//         // This is the part of your Slack URL, e.g., if your URL is https://mycompany.slack.com, the domain is 'mycompany'.
//         private const string SlackWorkspaceDomain = "your-workspace-domain";
//
//         // The channel name (or snippet/wildcard) you want to find the URL for.
//         // This will now match any channel containing this string, e.g., "prod" matches "#production-alerts" or "#qa-prod".
//         private const string TargetChannelName = "prod";
//
//         public static async Task Main(string[] args)
//         {
//             Console.WriteLine($"Searching for all channels matching snippet: '{TargetChannelName}' in workspace: {SlackWorkspaceDomain}.slack.com");
//
//             if (SlackBotToken.Contains("YOUR-SLACK-BOT-TOKEN") || SlackWorkspaceDomain.Contains("your-workspace-domain"))
//             {
//                 Console.ForegroundColor = ConsoleColor.Yellow;
//                 Console.WriteLine("\n*** ERROR: Please update SLACK_BOT_TOKEN and SLACK_WORKSPACE_DOMAIN in Program.cs with your actual values. ***");
//                 Console.ResetColor();
//                 return;
//             }
//
//             try
//             {
//                 var finder = new ChannelFinder(SlackBotToken, SlackWorkspaceDomain);
//
//                 // The method now returns a list of all matching channel URLs.
//                 var channelUrls = await finder.GetChannelUrlsByNameSnippetAsync(TargetChannelName);
//
//                 if (channelUrls.Count > 0)
//                 {
//                     Console.ForegroundColor = ConsoleColor.Green;
//                     Console.WriteLine($"\n✅ SUCCESS! Found {channelUrls.Count} matching channel(s):");
//                     Console.ResetColor();
//
//                     var index = 1;
//                     foreach (var url in channelUrls)
//                     {
//                         Console.WriteLine($"  {index++}. {url}");
//                     }
//                 }
//                 else
//                 {
//                     Console.ForegroundColor = ConsoleColor.Red;
//                     Console.WriteLine($"\n❌ ERROR: Could not find any channel whose name contains '{TargetChannelName}'.");
//                     Console.WriteLine("Ensure the channel exists and your bot token has the necessary 'channels:read' and 'groups:read' scopes.");
//                     Console.ResetColor();
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Console.ForegroundColor = ConsoleColor.Red;
//                 Console.WriteLine($"\nAn error occurred during the API call: {ex.Message}");
//                 Console.WriteLine("Check your API token, network connection, and permissions.");
//                 Console.ResetColor();
//             }
//         }
//     }
//
//     /// <summary>
//     ///     Handles interaction with the Slack API to find channel information.
//     /// </summary>
//     public class ChannelFinder
//     {
//         private readonly string _apiBaseUrl = "https://slack.com/api/";
//         private readonly HttpClient _httpClient;
//         private readonly string _workspaceDomain;
//
//         /// <summary>
//         ///     Initializes a new instance of the ChannelFinder class.
//         /// </summary>
//         /// <param name="slackBotToken">The Bot User OAuth Token (xoxb-...).</param>
//         /// <param name="workspaceDomain">The subdomain of your Slack workspace (e.g., 'mycompany').</param>
//         public ChannelFinder(string slackBotToken, string workspaceDomain)
//         {
//             this._workspaceDomain = workspaceDomain;
//             this._httpClient = new HttpClient();
//
//             // Set up Authorization header with the Bearer token
//             this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", slackBotToken);
//         }
//
//         /// <summary>
//         ///     Fetches the URLs for all Slack channels whose name contains the provided snippet (wildcard search).
//         ///     It uses conversations.list, iterates through all pages, and applies client-side string matching.
//         /// </summary>
//         /// <param name="channelNameSnippet">A snippet of the channel name (e.g., "prod"). Do not include the '#' prefix.</param>
//         /// <returns>A list of constructed deep link URLs for all matching channels. Returns an empty list if none are found.</returns>
//         public async Task<List<string>> GetChannelUrlsByNameSnippetAsync(string channelNameSnippet)
//         {
//             // Collection to store all matching channels found across all pages
//             var matchingChannels = new List<SlackChannel>();
//             string cursor = null;
//
//             do
//             {
//                 // The 'types' parameter includes both public and private channels.
//                 var url = $"{this._apiBaseUrl}conversations.list?types=public_channel,private_channel&limit=200";
//                 if (!string.IsNullOrEmpty(cursor))
//                 {
//                     url += $"&cursor={cursor}";
//                 }
//
//                 Console.WriteLine($"\n-> Calling conversations.list (Cursor: {(!string.IsNullOrEmpty(cursor) ? cursor : "Start")})...");
//
//                 var response = await this._httpClient.GetAsync(url);
//                 response.EnsureSuccessStatusCode(); // Throws an exception for HTTP errors
//
//                 var jsonString = await response.Content.ReadAsStringAsync();
//
//                 // Deserialize the response using the models defined in SlackResponseModels.cs
//                 var apiResponse = JsonSerializer.Deserialize<SlackConversationsListResponse>(jsonString);
//
//                 if (apiResponse == null || !apiResponse.ok)
//                 {
//                     // Log the specific Slack API error if available
//                     Console.WriteLine($"Slack API Error: {apiResponse?.error ?? "Unknown Error"}");
//                     return new List<string>(); // Return empty list on API error
//                 }
//
//                 // Iterate and collect all channels that match the snippet on this page
//                 foreach (var channel in apiResponse.channels)
//                 {
//                     // Check if the channel name contains the snippet (case-insensitive)
//                     if (channel.name.Contains(channelNameSnippet, StringComparison.OrdinalIgnoreCase))
//                     {
//                         matchingChannels.Add(channel);
//                         // We continue the loop to check the rest of the page and subsequent pages
//                     }
//                 }
//
//                 // Get the cursor for the next page, or null if this is the last page
//                 cursor = apiResponse.response_metadata?.next_cursor;
//             } while (!string.IsNullOrEmpty(cursor)); // Loop continues as long as a next_cursor is provided
//
//             // After collecting all matching channel IDs, construct the final list of URLs using LINQ.
//             // The URL format is: https://[workspace_domain].slack.com/archives/[channel_id]
//             var channelUrls = matchingChannels
//                 .Select(c => $"https://{this._workspaceDomain}.slack.com/archives/{c.id}")
//                 .ToList();
//
//             return channelUrls;
//         }
//     }
// }


