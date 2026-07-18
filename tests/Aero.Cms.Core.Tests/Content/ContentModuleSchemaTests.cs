using Aero.Cms.Core.Content;
using Aero.Cms.Modules.Content;
using AeroDB.Sable;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentModuleSchemaTests
{
    [Test]
    public async Task Content_type_schema_removes_the_retired_render_mode_field()
    {
        var options = new StoreOptions();

        new ContentModule().Configure(options);

        var mapping = options.Schema.For<ContentTypeDocument>();
        var retiredField = mapping.FieldDefinitions.Single(
            field => field.FieldName == "render_mode");

        await Assert.That(retiredField.Remove).IsTrue();
    }
}
