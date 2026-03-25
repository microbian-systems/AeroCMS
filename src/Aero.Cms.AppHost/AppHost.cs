var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Aero_Cms>("aero-cms-manager");

builder.AddProject<Projects.Aero_Cms_Web>("aero-cms-web")
    //.WithHttpsEndpoint(port:333, name: "static")
    ;

builder.Build().Run();
