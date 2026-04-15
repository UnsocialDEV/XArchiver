namespace XArchiver.Services;

public interface IResourceService
{
    string Format(string key, params object[] arguments);

    string GetString(string key);
}
