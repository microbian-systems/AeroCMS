using System;
using System.Collections.Generic;
using System.Text;
using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;

namespace Aero.Cms.Modules.Forum;

/// <summary>
/// reddit style forum module for async discussions
/// </summary>
public class AeroForumModule : AeroModuleBase
{
    public override string Name { get; } = nameof(AeroForumModule);
    public override string Version { get; } = AeroConstants.Version;
    public override string Author { get; } = AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies { get; } = [];
    public override IReadOnlyList<string> Category { get; } = [];
    public override IReadOnlyList<string> Tags { get; } = [];
}