using System.Text.Json;
using System.Text.Json.Serialization;

namespace XArchiver.Services;

public sealed class ScraperSessionStore : IScraperSessionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
    private readonly string _metadataPath;
    private readonly string _sessionRootDirectory;
    private readonly string _userDataDirectory;

    public ScraperSessionStore(string sessionRootDirectory)
    {
        _sessionRootDirectory = sessionRootDirectory;
        _userDataDirectory = Path.Combine(_sessionRootDirectory, "user-data");
        _metadataPath = Path.Combine(_sessionRootDirectory, "session.json");
    }

    public string GetUserDataDirectory()
    {
        Directory.CreateDirectory(_userDataDirectory);
        return _userDataDirectory;
    }

    public ScraperBrowserSessionInfo? GetSessionInfo()
    {
        if (!File.Exists(_metadataPath))
        {
            return null;
        }

        string json = File.ReadAllText(_metadataPath);
        ScraperBrowserSessionInfo? sessionInfo;
        try
        {
            sessionInfo = JsonSerializer.Deserialize<ScraperBrowserSessionInfo>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (sessionInfo is null)
        {
            return null;
        }

        return sessionInfo with
        {
            UserDataDirectory = string.IsNullOrWhiteSpace(sessionInfo.UserDataDirectory)
                ? GetUserDataDirectory()
                : sessionInfo.UserDataDirectory,
        };
    }

    public void ResetSession()
    {
        if (Directory.Exists(_userDataDirectory))
        {
            Directory.Delete(_userDataDirectory, recursive: true);
        }

        if (File.Exists(_metadataPath))
        {
            File.Delete(_metadataPath);
        }

        Directory.CreateDirectory(_sessionRootDirectory);
    }

    public void SaveSessionInfo(ScraperBrowserSessionInfo sessionInfo)
    {
        Directory.CreateDirectory(_sessionRootDirectory);

        string normalizedUserDataDirectory = string.IsNullOrWhiteSpace(sessionInfo.UserDataDirectory)
            ? GetUserDataDirectory()
            : sessionInfo.UserDataDirectory;

        string json = JsonSerializer.Serialize(
            sessionInfo with
            {
                UserDataDirectory = normalizedUserDataDirectory,
            },
            SerializerOptions);

        File.WriteAllText(_metadataPath, json);
    }
}
