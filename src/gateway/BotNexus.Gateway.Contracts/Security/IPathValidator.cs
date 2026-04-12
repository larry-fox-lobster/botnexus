namespace BotNexus.Gateway.Abstractions.Security;

/// <summary>
/// Defines the contract for ipath validator.
/// </summary>
public interface IPathValidator
{
    bool CanRead(string absolutePath);
    bool CanWrite(string absolutePath);
    string? ValidateAndResolve(string rawPath, FileAccessMode mode);
}

/// <summary>
/// Specifies supported values for file access mode.
/// </summary>
public enum FileAccessMode
{
    Read,
    Write
}
