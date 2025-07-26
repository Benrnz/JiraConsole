using System.Text.Json.Serialization;

public class JiraResponseDto
{
    [JsonPropertyName("issues")]
    public List<IssueDto> Issues { get; set; }

    [JsonPropertyName("isLast")]
    public bool IsLastPage { get; set; }

    public class IssueDto
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("fields")]
        public FieldsDto Fields { get; set; }
    }

    public class FieldsDto
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("status")]
        public StatusDto Status { get; set; }

        [JsonPropertyName("assignee")]
        public AssigneeDto Assignee { get; set; }

        [JsonPropertyName("customfield_11986")]
        public float? IsRequiredForGoLive { get; set; }
    }

    public class StatusDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class AssigneeDto
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }
    }
}
