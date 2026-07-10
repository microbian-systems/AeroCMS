// This file is hand-authored in the shim project to expose AeroCMS's
// block JSON serializer context to AeroDB's serializer configuration.
//
// Ideally this would use a source-generated JsonSerializerContext emitted
// by BlockRendererGenerator and consumed by STJ's JsonSourceGenerator.
// However, Roslyn source generators cannot chain: one generator's output
// is not visible to another in the same compilation (dotnet/roslyn#57239).
//
// Until that limitation is resolved, we wrap the hand-maintained
// BlockJsonContext from Aero.Cms.Abstractions.
//
// See docs/source-generator-chaining-limitation.md for details.

using System.Text.Json;
using System.Text.Json.Serialization;
using AeroDB.Sable;
using Aero.Cms.Abstractions.Blocks.Serialization;

namespace Aero.Cms.Generated;

/// <summary>
/// Extension methods that wire <see cref="BlockJsonContext.Default"/>
/// into AeroDB's serializer pipeline.
/// </summary>
/// <remarks>
/// When the Roslyn generator-chaining limitation is resolved, this will
/// switch to using <c>GeneratedBlockJsonContext.Default</c> produced
/// by the STJ source generator from
/// <c>BlockRendererGenerator</c>-emitted <c>[JsonSerializable]</c>
/// attributes.
/// </remarks>
public static class GeneratedAeroDbConfiguration
{
    /// <summary>
    /// Configures AeroDB to use <see cref="BlockJsonContext.Default"/> as
    /// its JSON serializer resolver for AOT-safe block type serialization.
    /// </summary>
    public static StoreOptions UseAeroGeneratedJsonContext(this StoreOptions options)
    {
        options.ConfigureSerializer(stj =>
        {
            stj.TypeInfoResolver = BlockJsonContext.Default;
            stj.AllowOutOfOrderMetadataProperties = true;
        });
        return options;
    }

    /// <summary>
    /// Creates a <see cref="JsonSerializerOptions"/> configured with
    /// <see cref="BlockJsonContext.Default"/> for AOT-safe block serialization.
    /// </summary>
    public static JsonSerializerOptions CreateAeroJsonOptions()
    {
        return new JsonSerializerOptions
        {
            TypeInfoResolver = BlockJsonContext.Default,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
}
