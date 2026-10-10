using System.IO;
using System.Security.Cryptography;
using System.Text;
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

    private const string ProtectedPrefix = "dpapi:";

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

            if (loaded is null)
                return;

            var foundPlainText = false;
            Transform(loaded, value => Unprotect(value, ref foundPlainText));
            Current = loaded;

            if (foundPlainText)
                WriteFile();
        }
        catch (Exception)
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        WriteFile();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void WriteFile()
    {
        try
        {
            var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(Current, Options), Options)
                       ?? new AppSettings();

            Transform(copy, Protect);
            File.WriteAllText(_path, JsonSerializer.Serialize(copy, Options));
        }
        catch (Exception)
        {
        }
    }

    private static void Transform(AppSettings settings, Func<string, string> map)
    {
        settings.AzureKey = map(settings.AzureKey);
        settings.DeepLKey = map(settings.DeepLKey);
        settings.LibreTranslateKey = map(settings.LibreTranslateKey);

        foreach (var profile in settings.CloudAiProfiles.Values)
            profile.Key = map(profile.Key);
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value) || value.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
            return value;

        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        return ProtectedPrefix + Convert.ToBase64String(encrypted);
    }

    private static string Unprotect(string value, ref bool foundPlainText)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (!value.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            foundPlainText = true;
            return value;
        }

        try
        {
            var bytes = Convert.FromBase64String(value[ProtectedPrefix.Length..]);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public void Replace(AppSettings settings)
    {
        Current = settings;
        Save();
    }
}
