using System.IO.Compression;
using System.Text;
using Aero.Core;
using Aero.Cms.Modules.Setup.Services;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Tests.Localization;

public sealed class TranslationImportPayloadReaderTests
{
    [Test]
    public async Task ReadAsync_ParsesSingleJsonPayload()
    {
        var json = """
            {
              "culture": "es-MX",
              "pages": [
                {
                  "sourceId": 100,
                  "slug": "acerca-de",
                  "title": "Acerca de"
                }
              ],
              "products": [
                {
                  "productId": 200,
                  "name": "Tema inicial",
                  "shortDescription": "Resumen localizado"
                }
              ]
            }
            """;

        var result = await TranslationImportPayloadReader.ReadAsync(
            "translations.json",
            Encoding.UTF8.GetBytes(json),
            CancellationToken.None);

        var ok = result as Result<List<TranslationImportPayload>, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).Count().IsEqualTo(1);
        await Assert.That(ok.Value[0].Culture).IsEqualTo("es-MX");
        await Assert.That(ok.Value[0].Pages[0].Slug).IsEqualTo("acerca-de");
        await Assert.That(ok.Value[0].Products[0].Name).IsEqualTo("Tema inicial");
    }

    [Test]
    public async Task ReadAsync_ParsesJsonPayloadsFromZip()
    {
        var zipBytes = CreateZip(
            "es/translations.json",
            """
            [
              {
                "culture": "es-MX",
                "categories": [
                  {
                    "categoryId": 300,
                    "name": "Noticias",
                    "slug": "noticias"
                  }
                ]
              }
            ]
            """);

        var result = await TranslationImportPayloadReader.ReadAsync(
            "translations.zip",
            zipBytes,
            CancellationToken.None);

        var ok = result as Result<List<TranslationImportPayload>, AeroError>.Ok;
        await Assert.That(ok).IsNotNull();
        await Assert.That(ok!.Value).Count().IsEqualTo(1);
        await Assert.That(ok.Value[0].Categories[0].CategoryId).IsEqualTo(300);
        await Assert.That(ok.Value[0].Categories[0].Slug).IsEqualTo("noticias");
    }

    [Test]
    public async Task ReadAsync_RejectsUnsupportedFiles()
    {
        var result = await TranslationImportPayloadReader.ReadAsync(
            "translations.csv",
            Encoding.UTF8.GetBytes("culture,sourceId"),
            CancellationToken.None);

        await Assert.That(result).IsTypeOf<Result<List<TranslationImportPayload>, AeroError>.Failure>();
    }

    private static byte[] CreateZip(string entryName, string content)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        return stream.ToArray();
    }
}
