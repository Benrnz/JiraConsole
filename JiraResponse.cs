using System.Text.Json.Serialization;

public class JiraResponse
{
    [JsonPropertyName("issues")]
    public List<Issue> Issues { get; set; }

    public class Issue
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("fields")]
        public Fields Fields { get; set; }
    }

    public class Fields
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("status")]
        public Status Status { get; set; }

        [JsonPropertyName("assignee")]
        public Assignee Assignee { get; set; }
    }

    public class Status
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class Assignee
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }
    }
}