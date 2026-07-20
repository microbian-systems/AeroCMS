using Aero.Cms.Core;
using Aero.Modular;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aero.Cms.Modules.Mcp;

/// <summary>
/// Declares MCP-related metadata for Aero CMS module discovery.
/// </summary>
/// <remarks>
/// The current implementation registers no MCP server, transport, tools, prompts, resources, endpoints, or request
/// handlers. It also establishes no authentication, authorization, tenant/user scope, schema validation, sandbox,
/// logging, data-access boundary, or read-only guarantee. Referencing MCP packages and advertising module metadata
/// does not make an MCP service available.
/// </remarks>
[Module(nameof(AeroMcpModule))]
public class AeroMcpModule : AeroModuleBase
{
        /// <summary>
    /// Gets the fixed name used to discover this module.
    /// </summary>
public override string Name => nameof(AeroMcpModule);
        /// <summary>
    /// Gets the Aero CMS version reported by this module.
    /// </summary>
public override string Version => AeroConstants.Version;
        /// <summary>
    /// Gets the Aero CMS author metadata reported by this module.
    /// </summary>
public override string Author => AeroConstants.Author;

        /// <summary>
    /// Gets descriptive discovery text expressing the intended MCP feature; it is not a runtime capability contract.
    /// </summary>
public override string Description =>
        "an MCP server for your aero instance. it can answer questions based on what you allow it to";
        /// <summary>
    /// Gets an empty module dependency list.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets the AI and tools discovery categories.
    /// </summary>
public override IReadOnlyList<string> Category => ["ai", "tools"];
        /// <summary>
    /// Gets the AI and MCP discovery tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["ai", "mcp"];

        /// <summary>
    /// Performs no service registration.
    /// </summary>
    /// <param name="services">The service collection, which this implementation leaves unchanged.</param>
    /// <param name="config">Unused configuration.</param>
    /// <param name="env">Unused host environment.</param>
    /// <remarks>
    /// The method is synchronous, has no cancellation or failure mapping, and does not call MCP registration
    /// extensions. Any server lifecycle, transport failures, or tool execution behavior must be provided elsewhere.
    /// </remarks>
public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {

    }
}
