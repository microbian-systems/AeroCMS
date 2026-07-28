namespace Aero.Cms.Abstractions.Ai;

/// <summary>
/// Identifies an AI provider across manager HTTP contracts.
/// </summary>
/// <remarks>
/// This transport type deliberately lives in the browser-safe abstractions
/// assembly. Server AI runtimes map it to their internal provider type at the
/// module boundary instead of exposing the server AI dependency to WebAssembly.
/// </remarks>
public enum AiProviderKind
{
    OpenAi = 0,
    Anthropic = 1,
    Google = 2,
    Groq = 3,
    DeepSeek = 4,
    MiniMax = 5,
    Mistral = 6,
    XAi = 7,
    Zai = 8,
    Perplexity = 9,
    Alibaba = 10,
    OpenRouter = 11,
    LmStudio = 50,
    OpenCode = 80,
    Future = 99
}
