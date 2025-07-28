using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A custom date time converter to cope with Atlassian using a wierd non-ISO8601 date format specifying a timezone without a colon.
/// For example: Standard ISO-8601: 2023-09-11T10:00:00.000+13:00
///                      Atlassian: 2023-09-11T10:00:00.000+1300
/// </summary>
public class CustomDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();
        if (string.IsNullOrEmpty(dateString))
        {
            return default;
        }

        // Handle timezone format without colon (e.g., +1300 -> +13:00)
        if (dateString.Length > 5 && (dateString.EndsWith("+") || dateString.Contains("+") || dateString.Contains("-")))
        {
            var lastPlusOrMinus = Math.Max(dateString.LastIndexOf('+'), dateString.LastIndexOf('-'));
            if (lastPlusOrMinus > 0)
            {
                var timezonePart = dateString.Substring(lastPlusOrMinus);
                if (timezonePart.Length == 5 && !timezonePart.Contains(':'))
                {
                    // Insert colon in timezone (e.g., +1300 -> +13:00)
                    var fixedTimezone = timezonePart.Substring(0, 3) + ":" + timezonePart.Substring(3);
                    dateString = dateString.Substring(0, lastPlusOrMinus) + fixedTimezone;
                }
            }
        }

        return DateTimeOffset.Parse(dateString, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"));
    }
}
