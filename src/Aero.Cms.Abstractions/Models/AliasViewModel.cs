using Aero.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Abstractions.Models;

public class AliasViewModel : Entity
{
    /// <summary>
    /// Gets or sets the unique identifier for the site.
    /// </summary>
    public long SiteId { get; set; }
    /// <summary>
    /// Gets or sets the original file or directory path before a rename or move operation.
    /// </summary>
    public string OldPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets the new file or directory path to be used in the operation.
    /// </summary>
    public string NewPath { get; set; } = null!;
    /// <summary>
    /// Gets or sets optional notes or comments associated with the object.
    /// </summary>
    public string? Notes { get; set; } = null!;
}
