namespace BensJiraConsole;

public class FieldMapping<T> : IFieldMapping
{
    private string? fieldName;

    private string FieldName => this.fieldName ??= string.IsNullOrEmpty(Alias) ? Field : Alias;

    public required string Field { get; init; }
    public string Alias { get; set; } = string.Empty;
    public string FlattenField { get; set; } = string.Empty;

    public virtual T? Parse(dynamic d)
    {
        if (d is IDictionary<string, object> dictionary && dictionary.TryGetValue(FieldName, out var value))
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (value is null)
            {
                return default;
            }

            return (T)value;
        }

        return default;
    }
}

public class FieldMappingWithParser<T> : FieldMapping<T>
{
    public required Func<dynamic, T?> Parser { get; init; }

    public override T? Parse(dynamic d)
    {
        return Parser(d);
    }
}
