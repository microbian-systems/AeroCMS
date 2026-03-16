var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Aero_Cms>("aero-cms");

builder.AddProject<Projects.Aero_Cms_Web>("aero-cms-web");

builder.Build().Run();
