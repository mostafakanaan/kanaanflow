namespace KanaanFlow.Web.Services;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DeviceSettings))]
internal sealed partial class WebJsonContext : JsonSerializerContext
{
}
