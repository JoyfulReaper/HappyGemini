using System.Text.Json;
using System.Text.Json.Serialization;

namespace HappyGemini.Events;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(GeminiServiceStartedEvent))]
[JsonSerializable(typeof(GeminiPageServedEvent))]
internal sealed partial class HappyGeminiJsonContext : JsonSerializerContext;
