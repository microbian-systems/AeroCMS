using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Core;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.Core;
using JasperFx.Core.Reflection;
using Marten;
using Marten.Schema;
using Marten.Schema.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    public override void ConfigureServices(IServiceCollection services, IConfiguration? config = null, IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);
        services.AddScoped<ISiteLookupService, SiteLookupService>();
    }

    public void Configure(IServiceProvider services, StoreOptions opts)
    {
        opts.Schema.For<SitesModel>().Identity(x => x.Id);
        opts.Schema.For<SitesModel>().IdStrategy(new SnowflakeIdGeneration());
        opts.Schema.For<SitesModel>().UniqueIndex(x => x.Name);
        opts.Schema.For<SitesModel>().UniqueIndex(x => x.Hostname);
        opts.Schema.For<SitesModel>().Index(x => x.IsEnabled);
        opts.Schema.For<SitesModel>().Index(x => x.CreatedOn);
        opts.Schema.For<SitesModel>().Index(x => x.ModifiedOn);
    }
}

public class SnowflakeIdGeneration : IIdGeneration
{
    public bool IsNumeric => true;
    public void GenerateCode(GeneratedMethod method, DocumentMapping mapping)
    {
        // Get the Id member (property/field)
        var idMember = mapping.IdMember;

        // This is the variable name Marten uses internally for the document
        var document = new Use(mapping.DocumentType);

        // Generate code:
        method.Frames.Code(
            $"if ({{0}}.{mapping.IdMember.Name} == 0) _setter({{0}}, {typeof(Snowflake).FullNameInCode()}.NewId());",
            document);
        method.Frames.Code($"return {{0}}.{mapping.IdMember.Name};", document);
    }
}
}