using System.Text.Json;
using System.Text.Json.Serialization;

public class ArrayToConcatenatedStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return string.Empty;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? string.Empty;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return ReadObjectValue(ref reader);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected array or string");
        }

        var values = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                values.Add(ReadObjectValue(ref reader));
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                values.Add(reader.GetString() ?? string.Empty);
            }
        }

        return string.Join(", ", values);
    }

    private string ReadObjectValue(ref Utf8JsonReader reader)
    {
        string? value = null;
        string? name = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read(); // Move to the value

                if (propertyName == "value")
                {
                    value = reader.GetString();
                }
                else if (propertyName == "name")
                {
                    name = reader.GetString();
                }
            }
        }

        return value ?? name ?? string.Empty;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
