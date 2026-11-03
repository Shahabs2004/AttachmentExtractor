using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AttachmentExtractor.Core.Services;

public static class JsonFileStore
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Load<T>(string path, Func<T> defaultFactory)
    {
        try
        {
            if (!File.Exists(path))
            {
                return defaultFactory();
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(json, Options) ?? defaultFactory();
        }
        catch
        {
            return defaultFactory();
        }
    }

    public static void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, Options);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json, new UTF8Encoding(false));
        File.Move(tempPath, path, true);
    }
}

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AttachmentExtractor");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string ProfilesFile => Path.Combine(DataDirectory, "profiles.json");

    public static string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
