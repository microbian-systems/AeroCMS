using Aero.Core.Ai;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

using DefaultAiProviderProfiles = Aero.Core.Ai.DefaultAiProviderProfiles;

namespace Aero.Cms.Modules.Ai.Tests;

public sealed class DefaultAiProviderProfilesTests
{
    [Test]
    public async Task Create_ShouldIncludeConfigurableRemoteProvidersAndLocalProvider()
    {
        var profiles = DefaultAiProviderProfiles.Create();

        profiles.Should().Contain(profile => profile.Id == "openai" && profile.Provider == AiProviderKind.OpenAi);
        profiles.Should().Contain(profile => profile.Id == "anthropic" && profile.Provider == AiProviderKind.Anthropic);
        profiles.Should().Contain(profile => profile.Id == "openrouter" && profile.Provider == AiProviderKind.OpenRouter);
        profiles.Should().Contain(profile => profile.Id == "lm-studio" && profile.Provider == AiProviderKind.LmStudio);
        profiles.Should().Contain(profile =>
            profile.Id == "opencode"
            && profile.Provider == AiProviderKind.OpenCode
            && profile.SupportsContentEnhancement);
        await Assert.That(profiles.Count).IsGreaterThanOrEqualTo(14);
    }

    [Test]
    public async Task Create_ShouldApplyConfiguredProviderDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Providers:OpenAi:Model"] = "gpt-4.1-mini",
                ["Ai:Providers:OpenAi:DisplayName"] = "Site OpenAI",
                ["Ai:Providers:OpenAi:Enabled"] = "true"
            })
            .Build();

        var openAi = DefaultAiProviderProfiles.Create(config)
            .Single(profile => profile.Id == DefaultAiProviderProfiles.OpenAiProviderId);

        openAi.DisplayName.Should().Be("Site OpenAI");
        openAi.Model.Should().Be("gpt-4.1-mini");
        openAi.Enabled.Should().BeTrue();
        await Assert.That(DefaultAiProviderProfiles.GetDefaultProviderId(config)).IsEqualTo("opencode");
    }
}
