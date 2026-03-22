using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Aero.Core.Entities;

namespace Aero.Cms.Core.Blocks.Editing;

/// <summary>
/// Default implementation of <see cref="IBlockMetadataProvider"/> that provides
/// cached block metadata using reflection.
/// </summary>
public sealed class BlockMetadataProvider : IBlockMetadataProvider
{
    private readonly ConcurrentDictionary<Type, BlockEditorMetadata?> _cache = new();
    private readonly List<BlockEditorMetadata> _allMetadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockMetadataProvider"/> class.
    /// </summary>
    /// <param name="assemblyMarkerTypes">Types from assemblies to scan for blocks.</param>
    public BlockMetadataProvider(IEnumerable<Type> assemblyMarkerTypes)
    {
        _allMetadata = DiscoverBlocks(assemblyMarkerTypes.Select(t => t.Assembly).Distinct().ToArray());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockMetadataProvider"/> class.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for blocks.</param>
    public BlockMetadataProvider(IEnumerable<Assembly> assemblies)
    {
        _allMetadata = DiscoverBlocks(assemblies.Distinct().ToArray());
    }

    /// <inheritdoc />
    public BlockEditorMetadata? GetMetadata(Type blockType)
    {
        return _cache.GetOrAdd(blockType, type =>
        {
            var attribute = type.GetCustomAttribute<BlockMetadataAttribute>();
            if (attribute == null)
            {
                return null;
            }

            return new BlockEditorMetadata
            {
                Name = attribute.Name,
                DisplayName = attribute.DisplayName,
                Description = attribute.Description,
                Category = attribute.Category ?? string.Empty,
                Icon = attribute.Icon ?? string.Empty,
                SortOrder = attribute.SortOrder,
                Properties = ExtractPropertyMetadata(type)
            };
        });
    }

    /// <inheritdoc />
    public IReadOnlyList<BlockEditorMetadata> GetAllMetadata()
    {
        return _allMetadata;
    }

    private static List<BlockEditorMetadata> DiscoverBlocks(Assembly[] assemblies)
    {
        var blockTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(BlockBase)))
            .Select(t => new
            {
                Type = t,
                Attribute = t.GetCustomAttribute<BlockMetadataAttribute>()
            })
            .Where(x => x.Attribute != null)
            .Select(x => new BlockEditorMetadata
            {
                Name = x.Attribute!.Name,
                DisplayName = x.Attribute.DisplayName,
                Description = x.Attribute.Description,
                Category = x.Attribute.Category ?? string.Empty,
                Icon = x.Attribute.Icon ?? string.Empty,
                SortOrder = x.Attribute.SortOrder,
                Properties = ExtractPropertyMetadata(x.Type)
            })
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.DisplayName)
            .ToList();

        return blockTypes;
    }

    private static List<BlockPropertyMetadata> ExtractPropertyMetadata(Type blockType)
    {
        return blockType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType != typeof(BlockBase) && p.DeclaringType != typeof(Entity))
            .Select(p => new BlockPropertyMetadata
            {
                Name = p.Name,
                DisplayName = p.Name,
                PropertyType = GetPropertyTypeName(p),
                IsRequired = IsRequiredProperty(p),
                DefaultValue = GetDefaultValue(p),
                Options = null
            })
            .ToList();
    }

    private static string GetPropertyTypeName(PropertyInfo property)
    {
        var type = property.PropertyType;

        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            if (genericType == typeof(Nullable<>))
            {
                return Nullable.GetUnderlyingType(type)?.Name ?? "object";
            }
        }

        return type.Name.ToLowerInvariant() switch
        {
            "string" => "string",
            "int32" => "int",
            "int64" => "long",
            "boolean" => "bool",
            "double" => "double",
            "decimal" => "decimal",
            "datetime" => "datetime",
            _ => type.Name.ToLowerInvariant()
        };
    }

    private static bool IsRequiredProperty(PropertyInfo property)
    {
        if (property.PropertyType.IsValueType &&
            Nullable.GetUnderlyingType(property.PropertyType) == null)
        {
            return true;
        }

        if (property.PropertyType == typeof(string))
        {
            return false;
        }

        return !property.PropertyType.IsGenericType ||
               property.GetCustomAttribute<RequiredAttribute>() != null;
    }

    private static string? GetDefaultValue(PropertyInfo property)
    {
        var defaultAttr = property.GetCustomAttribute<DefaultValueAttribute>();
        if (defaultAttr != null)
        {
            return defaultAttr.Value?.ToString();
        }

        if (property.PropertyType.IsValueType &&
            Nullable.GetUnderlyingType(property.PropertyType) == null)
        {
            return "0";
        }

        return null;
    }
}
