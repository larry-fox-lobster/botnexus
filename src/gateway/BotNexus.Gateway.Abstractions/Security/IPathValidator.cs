namespace BotNexus.Gateway.Abstractions.Security;

public interface IPathValidator
{
    bool CanRead(string absolutePath);
    bool CanWrite(string absolutePath);
    string? ValidateAndResolve(string rawPath, FileAccessMode mode);
}

public enum FileAccessMode
{
    Read,
    Write
}
