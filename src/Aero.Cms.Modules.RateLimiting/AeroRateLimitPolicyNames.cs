namespace Aero.Cms.Modules.RateLimiting;

/// <summary>
/// Stable policy names shared by AeroCMS AI, assistant, and MCP modules.
/// </summary>
public static class AeroRateLimitPolicyNames
{
    public const string AiPublic = "Aero.Ai.Public";
    public const string AiMember = "Aero.Ai.Member";
    public const string AiManager = "Aero.Ai.Manager";
    public const string AiStream = "Aero.Ai.Stream";
    public const string McpTransport = "Aero.Mcp.Transport";
    public const string McpManagement = "Aero.Mcp.Management";
    public const string McpRead = "Aero.Mcp.Read";
    public const string McpWrite = "Aero.Mcp.Write";
    public const string McpDestructive = "Aero.Mcp.Destructive";
}
