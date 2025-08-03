using System.Dynamic;
using System.Text.Json;
using BensJiraConsole.Tasks;

namespace BensJiraConsole;

public class JiraQueryDynamicRunner
{
    public string[] IgnoreFields => ["avatarId", "hierarchyLevel", "iconUrl", "id", "expand", "self", "subtask"];

    private SortedList<string, FieldMapping> fieldAliases = new();
    public async Task<List<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, FieldMapping[] fields)
    {
        var client = new JiraApiClient();
        var responseJson = await client.PostSearchJqlAsync(jql, fields.Select(x => x.Field).ToArray());

        this.fieldAliases = new SortedList<string, FieldMapping>(fields.ToDictionary(x => x.Field, x => x));

        var results = new List<dynamic>();
        using var doc = JsonDocument.Parse(responseJson);
        var issues = doc.RootElement.GetProperty("issues");
        foreach (var issue in issues.EnumerateArray())
        {
            results.Add(DeserializeToDynamic(issue, string.Empty));
        }

        return results;
    }

    private string FieldName(string field)
    {
        if (this.fieldAliases.TryGetValue(field, out var mapping))
        {
            return string.IsNullOrEmpty(mapping.Alias) ? field : mapping.Alias;
        }

        return field;
    }

    private bool PropertyShouldBeFlattened(string field, out string childField)
    {
        if (this.fieldAliases.TryGetValue(field, out var mapping))
        {
            childField = mapping.FlattenField;
            return !string.IsNullOrEmpty(mapping.FlattenField);
        }

        childField = string.Empty;
        return false;
    }

    private dynamic DeserializeToDynamic(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var expando = new ExpandoObject() as IDictionary<string, object>;
                foreach (var prop in element.EnumerateObject())
                {
                    if (IgnoreFields.Contains(prop.Name))
                    {
                        continue; // Skip fields that are in the ignore list
                    }

                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (PropertyShouldBeFlattened(prop.Name, out var childField))
                        {
                            // Extract the childField property from the issueType object
                            if (prop.Value.TryGetProperty(childField, out var childFieldValue) && childFieldValue.ValueKind == JsonValueKind.String)
                            {
                                expando[FieldName(prop.Name)] = childFieldValue.GetString();
                                continue;
                            }
                        }
                    }

                    expando[FieldName(prop.Name)] = DeserializeToDynamic(prop.Value, prop.Name);
                }

                return expando;
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(DeserializeToDynamic(item, propertyName));
                }

                if (PropertyShouldBeFlattened(propertyName, out var childField1))
                {
                    var flattened = list
                        .OfType<IDictionary<string, object>>()
                        .Select(obj => obj.TryGetValue("value", out var value) ? value : null)
                        .Where(x => x != null);
                    return string.Join(",", flattened);
                }

                return list;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                {
                    return l;
                }

                if (element.TryGetDouble(out var d))
                {
                    return d;
                }

                return element.GetDecimal();
            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                return element.GetRawText();
        }
    }
}
//     public async Task<List<JiraIssue>> SearchJiraIssuesWithJqlAsync(string jql, string[] fields)
//     {
//         var client = new JiraApiClient();
//         var responseJson = await client.PostSearchJqlAsync(jql, fields);
//         var mapper = new JiraIssueMapper();
//         var results = mapper.MapToJiraIssue(responseJson);
//         var totalResults = results.Count;
//         while (!mapper.WasLastPage)
//         {
//             Console.WriteLine($"    {totalResults} results fetched. Fetching next page of results...");
//             responseJson = await client.PostSearchJqlAsync(jql, fields, mapper.NextPageToken);
//             var moreResults = mapper.MapToJiraIssue(responseJson);
//             totalResults += moreResults.Count;
//             results.AddRange(moreResults);
//         }
//
//         if (totalResults > 100)
//         {
//             Console.WriteLine($"    {totalResults} total results fetched.");
//         }
//         return results;
//     }
