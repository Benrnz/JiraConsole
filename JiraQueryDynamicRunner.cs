using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BensJiraConsole;

public class JiraQueryDynamicRunner : IJiraQueryRunner
{
    private SortedList<string, IFieldMapping[]> fieldAliases = new();

    private string[] IgnoreFields => ["avatarId", "hierarchyLevel", "iconUrl", "id", "expand", "self", "subtask"];

    public async Task<AgileSprint?> GetCurrentSprintForBoard(int boardId)
    {
        var result = await new JiraApiClient().GetAgileBoardActiveSprintAsync(boardId);
        if (string.IsNullOrEmpty(result))
        {
            return null;
        }

        var json = JsonNode.Parse(result);
        if (json is null)
        {
            return null;
        }

        var values = json["values"]?[0] ?? throw new NotSupportedException("No Agile Sprint values returned from API.");
        return CreateAgileSprintFromJsonNode(values);
    }

    public async Task<IReadOnlyList<dynamic>> SearchJiraIssuesWithJqlAsync(string jql, IFieldMapping[] fields)
    {
        string? nextPageToken = null;
        bool isLastPage;
        var client = new JiraApiClient();
        var results = new List<dynamic>();

        this.fieldAliases = new SortedList<string, IFieldMapping[]>();
        // There might be more than one mapping per field.  This is because we might be flattening an object with many fields into a multiple top level properties.
        foreach (var field in fields)
        {
            if (this.fieldAliases.ContainsKey(field.Field))
            {
                this.fieldAliases[field.Field] = this.fieldAliases[field.Field].Append(field).ToArray();
            }
            else
            {
                this.fieldAliases.Add(field.Field, [field]);
            }
        }

        do
        {
            var responseJson = await client.PostSearchJqlAsync(jql, fields.Select(x => x.Field).ToArray(), nextPageToken);

            using var doc = JsonDocument.Parse(responseJson);
            var issues = doc.RootElement.GetProperty("issues");
            isLastPage = doc.RootElement.TryGetProperty("isLast", out var isLastPageToken) && isLastPageToken.GetBoolean();
            nextPageToken = doc.RootElement.TryGetProperty("nextPageToken", out var token) ? token.GetString() : null;

            foreach (var issue in issues.EnumerateArray())
            {
                results.Add(DeserializeToDynamic(issue, string.Empty));
            }

            Console.WriteLine($"    Fetched {results.Count} issues.");
        } while (!isLastPage || nextPageToken != null);

        return results;
    }

    public async Task<AgileSprint?> GetSprintById(int sprintId)
    {
        var result = await new JiraApiClient().GetAgileBoardSprintByIdAsync(sprintId);
        if (string.IsNullOrEmpty(result))
        {
            return null;
        }

        var json = JsonNode.Parse(result);
        if (json is null)
        {
            return null;
        }

        return CreateAgileSprintFromJsonNode(json);
    }

    private AgileSprint CreateAgileSprintFromJsonNode(JsonNode values)
    {
        return new AgileSprint(
            values["id"]!.GetValue<int>(),
            values["state"]!.GetValue<string>(),
            values["name"]!.GetValue<string>(),
            values["startDate"]!.GetValue<DateTimeOffset>(),
            values["endDate"]!.GetValue<DateTimeOffset>(),
            values["originBoardId"]!.GetValue<int>(),
            values["goal"]!.GetValue<string>());
    }

    private dynamic DeserialiseDynamicArray(JsonElement element, string propertyName, string childField)
    {
        var list = new List<object>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(DeserializeToDynamic(item, propertyName));
        }

        var flattened = list
            .OfType<IDictionary<string, object>>()
            .Select(obj => obj.TryGetValue(childField, out var value) ? value : null)
            .Where(x => x != null);
        return string.Join(",", flattened);
    }

    private IDictionary<string, object> DeserialiseDynamicObject(JsonElement element)
    {
        var expando = new ExpandoObject() as IDictionary<string, object>;
        foreach (var prop in element.EnumerateObject())
        {
            // Special handling for 'fields' property - it's useful to flatten it
            if (prop is { Name: "fields", Value.ValueKind: JsonValueKind.Object })
            {
                // Flatten 'fields' properties into the parent object
                foreach (var fieldProp in prop.Value.EnumerateObject())
                {
                    if (fieldProp.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (PropertyShouldBeFlattened(fieldProp.Name, out var childFields1))
                        {
                            foreach (var childField in childFields1)
                            {
                                // Extract the childField property from the issueType object
                                if (fieldProp.Value.TryGetProperty(childField, out var childFieldValue) && childFieldValue.ValueKind == JsonValueKind.String)
                                {
                                    expando[FieldName(fieldProp.Name, childField)] = childFieldValue.GetString()!;
                                }
                            }

                            continue;
                        }
                    }

                    if (PropertyShouldBeFlattened(fieldProp.Name, out var childFields2))
                    {
                        foreach (var childField in childFields2)
                        {
                            expando[FieldName(fieldProp.Name, childField)] = DeserializeToDynamic(fieldProp.Value, fieldProp.Name, childField);
                        }
                    }
                    else
                    {
                        expando[FieldName(fieldProp.Name)] = DeserializeToDynamic(fieldProp.Value, fieldProp.Name);
                    }
                }

                continue;
            }

            if (IgnoreFields.Contains(prop.Name))
            {
                continue; // Skip fields that are in the ignore list
            }

            expando[FieldName(prop.Name)] = DeserializeToDynamic(prop.Value, prop.Name);
        }

        return expando;
    }

    private dynamic DeserializeToDynamic(JsonElement element, string propertyName, string childField = "")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return DeserialiseDynamicObject(element);

            case JsonValueKind.Array:
                return DeserialiseDynamicArray(element, propertyName, childField);

            case JsonValueKind.String:
                var elementString = element.GetString();
                if (DateTimeOffset.TryParse(elementString, out var dto))
                {
                    return dto.ToLocalTime(); // Dates coming thru as UTC, convert to local time (is reversible and keeps timezone info)
                }

                return elementString!;

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
                return null!;

            default:
                return element.GetRawText();
        }
    }

    private string FieldName(string field, string childField = "")
    {
        if (this.fieldAliases.TryGetValue(field, out var mappings))
        {
            var mapping = mappings.FirstOrDefault(m => m.FlattenField == childField);
            if (mapping is null)
            {
                mapping = mappings.First();
            }

            return string.IsNullOrEmpty(mapping.Alias) ? field : mapping.Alias;
        }

        return field;
    }

    private bool PropertyShouldBeFlattened(string field, out IEnumerable<string> childFields)
    {
        if (this.fieldAliases.TryGetValue(field, out var mapping))
        {
            childFields = mapping.Select(m => m.FlattenField).Where(f => !string.IsNullOrEmpty(f));
            return childFields.Any();
        }

        childFields = Array.Empty<string>();
        return false;
    }
}
