var builder = DistributedApplication.CreateBuilder(args);

// Register the manager application as an independently orchestrated project resource.
builder.AddProject<Projects.Aero_Cms>("aero-cms-manager");

// Register the public web application as a second project resource. This AppHost does not
// declare project-to-project references, startup wait constraints, environment overrides,
// endpoint overrides, or persistence resources for either application.
builder.AddProject<Projects.Aero_Cms_Web>("aero-cms-web")
    ;

// Build the complete resource graph before starting the distributed application. This entry point
// does not add its own exception handling around host construction or Run.
builder.Build().Run();
