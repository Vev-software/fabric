using System.Text.Json;
using Json.Schema;

namespace Vev.Fabric.Contracts.Tests;

internal static class TestSchemas
{
    private static readonly object Gate = new();
    private static readonly SchemaRegistry Registry = new();
    private static readonly BuildOptions BuildOptions = new() { SchemaRegistry = Registry };
    private static readonly Dictionary<string, Uri> SchemaIds = new(StringComparer.OrdinalIgnoreCase);
    private static bool _registered;

    private static readonly string SchemaDir = Path.Combine(AppContext.BaseDirectory, "schemas", "v1");

    public static string SampleDir { get; } = Path.Combine(AppContext.BaseDirectory, "samples");

    public static JsonSchema Load(string entry)
    {
        EnsureRegistered();
        return (JsonSchema)Registry.Get(SchemaIds[entry])!;
    }

    private static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(SchemaDir, "*.json"))
            {
                var content = File.ReadAllText(file);
                using var document = JsonDocument.Parse(content);
                var id = new Uri(document.RootElement.GetProperty("$id").GetString()!, UriKind.Absolute);

                SchemaIds[Path.GetFileName(file)] = id;
                JsonSchema.FromText(content, BuildOptions);
            }

            _registered = true;
        }
    }
}
