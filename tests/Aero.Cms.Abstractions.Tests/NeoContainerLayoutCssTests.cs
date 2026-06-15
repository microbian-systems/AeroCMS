using System.Text.Json;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Tests;

public sealed class NeoContainerLayoutCssTests
{
    [Test]
    public async Task FromProperties_Defaults_To_Stack_Layout()
    {
        var css = NeoContainerLayoutCss.FromProperties(new Dictionary<string, JsonElement>());

        await Assert.That(css).IsEqualTo("w-full flex flex-col gap-4");
    }

    [Test]
    public async Task FromProperties_Uses_Bounded_Grid_Classes()
    {
        var properties = new Dictionary<string, JsonElement>
        {
            ["layout"] = JsonSerializer.SerializeToElement("grid"),
            ["columns"] = JsonSerializer.SerializeToElement(3),
            ["gap"] = JsonSerializer.SerializeToElement(6)
        };

        var css = NeoContainerLayoutCss.FromProperties(properties);

        await Assert.That(css).IsEqualTo("w-full grid grid-cols-3 gap-6");
    }

    [Test]
    public async Task FromProperties_Falls_Back_For_Non_Numeric_Gap_And_Columns()
    {
        var properties = new Dictionary<string, JsonElement>
        {
            ["layout"] = JsonSerializer.SerializeToElement("grid"),
            ["columns"] = JsonSerializer.SerializeToElement("3"),
            ["gap"] = JsonSerializer.SerializeToElement("4")
        };

        var css = NeoContainerLayoutCss.FromProperties(properties);

        await Assert.That(css).IsEqualTo("w-full grid grid-cols-1 gap-4");
    }
}
