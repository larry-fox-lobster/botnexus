using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BotNexus.Core.Abstractions;

/// <summary>
/// Optional interface that extension assemblies can implement to control
/// their own DI registration. If absent, the loader uses convention-based
/// registration by scanning for IChannel/ILlmProvider/ITool implementations.
/// </summary>
public interface IExtensionRegistrar
{
    void Register(IServiceCollection services, IConfiguration configuration);
}
