namespace KanaanFlow.Core.Finance.Events;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Source-generated JSON for the event log, safe for trimmed WebAssembly builds.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(BookEvent))]
[JsonSerializable(typeof(List<BookEvent>))]
public sealed partial class BookJsonContext : JsonSerializerContext
{
    public static string Serialize(BookEvent bookEvent) => JsonSerializer.Serialize(bookEvent, Default.BookEvent);

    public static BookEvent? Deserialize(string json) => JsonSerializer.Deserialize(json, Default.BookEvent);
}
