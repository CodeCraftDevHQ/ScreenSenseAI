using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? Changed;

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenTranslator");

        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "settings.json");
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;

            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);

            if (loaded is not null)
                Current = loaded;
        }
        catch (Exception)
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
        }
    }

    public void Replace(AppSettings settings)
    {
        Current = settings;
        Save();
    }
}
