using System.Text.Json;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Blocks.Dynamic;

public interface ISecureScribanRenderer
{
    Task<Result<string, AeroError>> RenderAsync(
        DynamicBlockDefinition definition,
        JsonDocument? data,
        CancellationToken cancellationToken = default);
}
