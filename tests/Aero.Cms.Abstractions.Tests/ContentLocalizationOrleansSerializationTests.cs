using Aero.Cms.Abstractions.Content.Localization;
using Aero.Cms.Abstractions.Models;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;
using Orleans.Serialization.Serializers;
using TUnit.Core;
using Shouldly;

namespace Aero.Cms.Abstractions.Tests;

public sealed class ContentLocalizationOrleansSerializationTests
{
    [Test]
    public void ContentTypeAndNestedLocalizationContractsHaveGeneratedCodecs()
    {
        var services = new ServiceCollection();
        services.AddSerializer(serializer => serializer.AddAssembly(typeof(ContentTypeViewModel).Assembly));
        using var provider = services.BuildServiceProvider();
        var codecs = provider.GetRequiredService<ICodecProvider>();

        codecs.GetCodec<ContentTypeViewModel>().ShouldNotBeNull();
        codecs.GetCodec<ContentLocalizationSettings>().ShouldNotBeNull();
        codecs.GetCodec<ContentTranslationProvenance>().ShouldNotBeNull();
        codecs.GetCodec<ContentTranslationReview>().ShouldNotBeNull();
    }
}
