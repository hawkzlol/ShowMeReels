using System.IO;
using System.Text.Json;
using ShowMeReels.App.Models;

namespace ShowMeReels.App.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;

    public JsonSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings().Normalize();
        }

        try
        {
            await using FileStream inputStream = File.OpenRead(_settingsPath);
            AppSettings? settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                inputStream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);

            return (settings ?? new AppSettings()).Normalize();
        }
        catch (IOException)
        {
            return new AppSettings().Normalize();
        }
        catch (JsonException)
        {
            return new AppSettings().Normalize();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        AppSettings normalizedSettings = settings.Normalize();
        string? settingsDirectory = Path.GetDirectoryName(_settingsPath);

        if (!string.IsNullOrWhiteSpace(settingsDirectory))
        {
            Directory.CreateDirectory(settingsDirectory);
        }

        string temporaryPath = $"{_settingsPath}.tmp";
        await using (FileStream outputStream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(outputStream, normalizedSettings, SerializerOptions, cancellationToken).ConfigureAwait(false);
            await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }
}
