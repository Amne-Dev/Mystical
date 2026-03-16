using System.Text.Json.Serialization;
using Mystical.WinUI.Models;

namespace Mystical.WinUI.Services;

[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext { }
