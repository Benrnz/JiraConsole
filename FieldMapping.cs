namespace BensJiraConsole;

public class FieldMapping
{
    private string? fieldName = null;

    public required string Field { get; init; }
    public string Alias { get; set; } = string.Empty;
    public string FlattenField { get; set; } = string.Empty;

    private string FieldName => this.fieldName ??= string.IsNullOrEmpty(Alias) ? Field : Alias;

    public virtual T Parse<T>(dynamic d)
    {
        if (d is IDictionary<string, object> dictionary && dictionary.TryGetValue(FieldName, out var value))
        {
            return (T)value;
        }

        return default;
    }
}

public class FieldMappingWithParser<T> : FieldMapping
{
    public required Func<dynamic, T> Parser { get; init; }

    public override T Parse<T>(dynamic d)
    {
        return Parser(d);
    }
}
