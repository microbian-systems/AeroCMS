using System.Globalization;
using Aero.Cms.Shared.Blocks.Rendering;

namespace Aero.Cms.Core.Tests.Localization;

public sealed class BlockRenderContextCultureTests
{
    [Test]
    public async Task Constructor_UsesCurrentCulture_WhenCultureIsNotProvided()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var culture = CultureInfo.GetCultureInfo("es-MX");
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            var context = new BlockRenderContext();

            await Assert.That(context.Culture.Name).IsEqualTo("es-MX");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    public async Task Constructor_UsesProvidedCulture_WhenCultureIsProvided()
    {
        var context = new BlockRenderContext(Culture: CultureInfo.GetCultureInfo("ar-SA"));

        await Assert.That(context.Culture.Name).IsEqualTo("ar-SA");
    }
}
