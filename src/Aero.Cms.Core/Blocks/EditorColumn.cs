using System;
using System.Collections.Generic;

namespace Aero.Cms.Core.Blocks;

/// <summary>Column within a Columns block in the editor.</summary>
public class EditorColumn
{
    public string           ColId  { get; set; } = Guid.NewGuid().ToString();
    public List<NestedBlock> Blocks { get; set; } = [];
}