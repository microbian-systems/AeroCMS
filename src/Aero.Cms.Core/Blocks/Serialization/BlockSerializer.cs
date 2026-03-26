using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Core.Railway;

namespace Aero.Cms.Core.Blocks.Serialization;

/// <summary>
/// Provides AOT-compatible JSON serialization for CMS blocks using System.Text.Json.
/// </summary>
public static class BlockSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = BlockJsonContext.Default,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Serializes a block to a JSON string.
    /// </summary>
    /// <param name="block">The block to serialize.</param>
    /// <returns>A JSON string representation of the block.</returns>
    public static string Serialize(BlockBase block)
    {
        return JsonSerializer.Serialize(block, Options);
    }

    /// <summary>
    /// Serializes a collection of blocks to a JSON string.
    /// </summary>
    /// <param name="blocks">The blocks to serialize.</param>
    /// <returns>A JSON string representation of the blocks.</returns>
    public static string Serialize(IEnumerable<BlockBase> blocks)
    {
        return JsonSerializer.Serialize(blocks, Options);
    }

    /// <summary>
    /// Deserializes a JSON string to a block.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized block, or an error result if deserialization fails.</returns>
    public static Result<string, BlockBase> Deserialize(string json)
    {
        try
        {
            var block = JsonSerializer.Deserialize<BlockBase>(json, Options);
            return block is null
                ? new Result<string, BlockBase>.Failure("Deserialization returned null")
                : block;
        }
        catch (JsonException ex)
        {
            return new Result<string, BlockBase>.Failure($"Failed to deserialize block: {ex.Message}");
        }
    }

    /// <summary>
    /// Deserializes a JSON string to a list of blocks.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized blocks, or an error result if deserialization fails.</returns>
    public static Result<string, IReadOnlyList<BlockBase>> DeserializeMany(string json)
    {
        try
        {
            var blocks = JsonSerializer.Deserialize<List<BlockBase>>(json, Options);
            return blocks is null
                ? new Result<string, IReadOnlyList<BlockBase>>.Failure("Deserialization returned null")
                : blocks;
        }
        catch (JsonException ex)
        {
            return new Result<string, IReadOnlyList<BlockBase>>.Failure($"Failed to deserialize blocks: {ex.Message}");
        }
    }

    /// <summary>
    /// Deserializes a JSON string to a block using the specified block type.
    /// </summary>
    /// <typeparam name="T">The expected block type.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized block, or an error result if deserialization fails.</returns>
    public static Result<string, T> Deserialize<T>(string json) where T : BlockBase
    {
        try
        {
            var block = JsonSerializer.Deserialize<T>(json, Options);
            return block is null
                ? new Result<string, T>.Failure("Deserialization returned null")
                : block;
        }
        catch (JsonException ex)
        {
            return new Result<string, T>.Failure($"Failed to deserialize {typeof(T).Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Tries to deserialize a JSON string to a block.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A tuple containing the deserialized block and whether deserialization succeeded.</returns>
    public static (BlockBase? Block, bool Success) TryDeserialize(string json)
    {
        try
        {
            var block = JsonSerializer.Deserialize<BlockBase>(json, Options);
            return (block, true);
        }
        catch (JsonException)
        {
            return (null, false);
        }
    }

    /// <summary>
    /// Gets the JSON bytes for a block.
    /// </summary>
    /// <param name="block">The block to serialize.</param>
    /// <returns>UTF-8 bytes representing the JSON.</returns>
    public static byte[] SerializeToBytes(BlockBase block)
    {
        return JsonSerializer.SerializeToUtf8Bytes(block, Options);
    }

    /// <summary>
    /// Deserializes UTF-8 bytes to a block.
    /// </summary>
    /// <param name="utf8Bytes">The UTF-8 bytes to deserialize.</param>
    /// <returns>The deserialized block, or an error result if deserialization fails.</returns>
    public static Result<string, BlockBase> DeserializeFromBytes(byte[] utf8Bytes)
    {
        try
        {
            var block = JsonSerializer.Deserialize<BlockBase>(utf8Bytes, Options);
            return block is null
                ? new Result<string, BlockBase>.Failure("Deserialization returned null")
                : block;
        }
        catch (JsonException ex)
        {
            return new Result<string, BlockBase>.Failure($"Failed to deserialize block: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the JSON element for a block (useful for polymorphic manipulation).
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>The parsed JSON element.</returns>
    public static JsonElement ParseElement(string json)
    {
        return JsonDocument.Parse(json, default).RootElement;
    }
}
