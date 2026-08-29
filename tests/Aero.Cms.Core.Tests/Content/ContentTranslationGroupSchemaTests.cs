using Aero.Cms.Modules.Content;
using TUnit.Core;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentTranslationGroupSchemaTests
{
    [Test]
    public async Task ContentModuleSchemaInitializesTranslationGroupDocument()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(new ContentModule().Configure);

        await harness.InitializeAsync();
    }
}
