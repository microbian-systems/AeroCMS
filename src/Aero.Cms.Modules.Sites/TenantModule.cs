using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Core.Entities;
using Marten;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aero.Cms.Modules.Sites;

public class SitesModule : AeroModuleBase, IConfigureMarten
{
    public override string Name => nameof(SitesModule);

    public override string Version => AeroVersion.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["mulit-site", "website"];

    public override IReadOnlyList<string> Tags => ["multi-site", "sites"];

    public void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<SitesModel>().Index(x => x.Name);
        opts.Schema.For<SitesModel>().Index(x => x.Hostname);
        opts.Schema.For<SitesModel>().Index(x => x.CreatedOn);
        opts.Schema.For<SitesModel>().Index(x => x.ModifiedOn);
    }
}


public class SitesModel : Entity
{
    public string Name { get; set; } = null!;
    public string Hostname { get; set; } = null!;
}