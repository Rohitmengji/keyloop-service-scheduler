using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ServiceScheduler.Api.Http;

/// <summary>
/// Accepts only ISO 8601 timestamps that carry an explicit offset ("Z" or "+hh:mm"). Without this, a value
/// such as "2026-11-02T09:00:00" would silently be read in the server's local time zone.
/// </summary>
internal sealed partial class ExplicitOffsetDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Expected =
        "Expected an ISO 8601 timestamp with a UTC offset, for example 2026-11-02T09:00:00+00:00 or 2026-11-02T09:00:00Z.";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(Expected);
        }

        var text = reader.GetString();
        if (text is null
            || !Iso8601WithOffset().IsMatch(text)
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new JsonException(Expected);
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Iso8601WithOffset();
}
