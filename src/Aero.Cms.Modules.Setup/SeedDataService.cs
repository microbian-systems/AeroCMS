using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Core;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Blocks.Layout;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Blog;
using Aero.Cms.Modules.Blog.Models;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Tenant;
using Aero.Cms.Web.Core.Modules;
using Aero.Core;
using Aero.Services.Images;
using Marten;
using Aero.Cms.Web.Core.Blocks;

namespace Aero.Cms.Modules.Setup;

public sealed record SeedDatabaseRequest(
    string DatabaseMode,
    string CacheMode,
    string SecretProvider,
    string AuthenticationMode,
    string? ConnectionString,
    string? CacheConnectionString,
    string? InfisicalMachineId,
    string? InfisicalClientSecret,
    string AdminUserName,
    string AdminEmail,
    string Password,
    string SiteName,
    string HomepageTitle,
    string BlogName,
    string Hostname,
    string DefaultCulture);

public sealed class SeedDatabaseResult
{
    public bool Succeeded => Errors.Count == 0;
    public bool AlreadyComplete { get; init; }
    public bool CreatedAdmin { get; init; }
    public bool CreatedRoles { get; init; }
    public bool CreatedTenant { get; init; }
    public bool CreatedSite { get; init; }
    public long? TenantId { get; init; }
    public long? SiteId { get; init; }
    public List<string> Errors { get; } = [];

    public static SeedDatabaseResult Failure(params string[] errors)
        => Failure(errors.AsEnumerable());

    public static SeedDatabaseResult Failure(IEnumerable<string> errors)
    {
        var result = new SeedDatabaseResult();
        result.Errors.AddRange(errors.Where(error => !string.IsNullOrWhiteSpace(error)));
        return result;
    }
}

public interface ISeedDatabaseService
{
    Task<SeedDatabaseResult> CompleteAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alias for backwards compatibility. ISeedDatabaseService was previously named ISetupCompletionService.
/// </summary>
public interface ISetupCompletionService : ISeedDatabaseService { }

public sealed class SeedDatabaseService(
    IDocumentSession session,
    ISetupIdentityBootstrapper identityBootstrapper,
    IPageContentService pageContentService,
    IBlogPostContentService blogPostContentService,
    IStaticPhotosClient staticPhotosClient,
    IModuleDiscoveryService moduleDiscoveryService,
    IModuleStateStore moduleStateStore,
    IBootstrapCompletionWriter bootstrapCompletionWriter,
    ITenantService tenantService,
    ISiteService siteService,
    IApiKeyService apiKeyService) : ISeedDatabaseService, ISetupCompletionService
{
    public async Task<SeedDatabaseResult> CompleteAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingState = await session.LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId, cancellationToken);
        if (existingState?.IsComplete == true)
        {
            return new SeedDatabaseResult
            {
                AlreadyComplete = true
            };
        }

        var identityResult = await identityBootstrapper.BootstrapAsync(
            new SetupIdentityBootstrapRequest(
                request.AdminUserName,
                request.AdminEmail,
                request.Password),
            cancellationToken);

        if (!identityResult.Succeeded)
        {
            return SeedDatabaseResult.Failure(identityResult.Errors.Select(error => error.Description));
        }

        // Create default admin API key
        // TODO: Remove this pre-defined key later once stable
        const string defaultAdminApiKey = "aero-admin-default-key-2025";
        await apiKeyService.CreateKeyAsync(identityResult.AdminUser!.Id, request.AdminEmail, defaultAdminApiKey, cancellationToken);

        // Create tenant and site for multi-tenant foundation
        var (tenantResult, siteResult) = await CreateTenantAndSiteAsync(request, cancellationToken);
        if (tenantResult.IsFailure || siteResult.IsFailure)
        {
            var errors = new List<string>();
            if (tenantResult is Result<TenantModel, AeroError>.Failure tenantFail)
                errors.Add(tenantFail.Error is AeroError.Error te ? te.msg : "Failed to create tenant");
            if (siteResult is Result<SitesModel, AeroError>.Failure siteFail)
                errors.Add(siteFail.Error is AeroError.Error se ? se.msg : "Failed to create site");
            return SeedDatabaseResult.Failure(errors);
        }

        var tenant = tenantResult is Result<TenantModel, AeroError>.Ok tenantOk ? tenantOk.Value : null;
        var site = siteResult is Result<SitesModel, AeroError>.Ok siteOk ? siteOk.Value : null;
        
        if (tenant == null || site == null)
        {
            return SeedDatabaseResult.Failure("Failed to create tenant or site");
        }

        try
        {
            await SeedStarterContentAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return SeedDatabaseResult.Failure(ex.Message);
        }

        var completedAtUtc = existingState?.CompletedAtUtc ?? DateTimeOffset.UtcNow;
        session.Store(new SetupStateDocument
        {
            Id = SetupStateDocument.FixedId,
            IsComplete = true,
            CompletedAtUtc = completedAtUtc,
            DatabaseMode = request.DatabaseMode,
            CacheMode = request.CacheMode,
            SecretProvider = request.SecretProvider,
            AdminEmail = request.AdminEmail,
            SiteName = request.SiteName,
            HomepageTitle = request.HomepageTitle,
            BlogName = request.BlogName,
            CreatedTenantId = tenant.Id,
            CreatedSiteId = site.Id,
            Hostname = request.Hostname,
            DefaultCulture = request.DefaultCulture
        });
        await session.SaveChangesAsync(cancellationToken);

        // Discover and save all available modules
        await SaveModuleStateAsync(cancellationToken);
        await bootstrapCompletionWriter.MarkCompleteAsync(cancellationToken);

        return new SeedDatabaseResult
        {
            CreatedAdmin = identityResult.CreatedAdmin,
            CreatedRoles = identityResult.CreatedRoles,
            CreatedTenant = true,
            CreatedSite = true,
            TenantId = tenant.Id,
            SiteId = site.Id
        };
    }

    private async Task<(Result<TenantModel, AeroError> Tenant, Result<SitesModel, AeroError> Site)> CreateTenantAndSiteAsync(
        SeedDatabaseRequest request, 
        CancellationToken cancellationToken)
    {
        // Create tenant with SiteName as the tenant name
        var tenant = new TenantModel
        {
            Id = Snowflake.NewId(),
            Name = request.SiteName,
            Hostname = request.Hostname,
            Notes = $"Default tenant created during setup on {DateTimeOffset.UtcNow:yyyy-MM-dd}"
        };

        var tenantResult = await tenantService.CreateTenantAsync(tenant, cancellationToken);
        
        if (tenantResult.IsFailure)
        {
            var tenantError = tenantResult is Result<TenantModel, AeroError>.Failure tf 
                ? (tf.Error is AeroError.Error te ? te.msg : "Failed to create tenant")
                : "Failed to create tenant";
            return (tenantResult, new Result<SitesModel, AeroError>.Failure(AeroError.CreateError(tenantError)));
        }

        // Get the created tenant's ID
        var createdTenantId = tenantResult is Result<TenantModel, AeroError>.Ok to 
            ? to.Value.Id 
            : tenant.Id;

        // Create site linked to the tenant
        var site = new SitesModel
        {
            Id = Snowflake.NewId(),
            TenantId = createdTenantId,
            Name = request.SiteName,
            Hostname = request.Hostname,
            IsEnabled = true,
            DefaultCulture = request.DefaultCulture
        };

        var siteResult = await siteService.CreateSiteAsync(site, cancellationToken);
        
        return (tenantResult, siteResult);
    }

    private async Task SeedStarterContentAsync(SeedDatabaseRequest request, CancellationToken cancellationToken)
    {
        // Build pages first to get their IDs for navigation items
        var (homepage, homepageBlocks) = BuildHomepage(request);
        var (blogListing, blogListingBlocks) = BuildBlogListingPage(request);
        var (aboutPage, aboutBlocks) = BuildAboutPage();
        var (contactPage, contactBlocks) = BuildContactPage();
        var docs = BuildStarterDocsContent();
        var rootDoc = docs.First(d => d.Slug == "docs");

        // Create main navigation menu
        var mainNav = new NavigationBlock
        {
            Id = Snowflake.NewId(),
            Name = "Main Navigation",
            Items =
            {
                { 0, new NavigationBlock.NavigationBlockItem { Id = Snowflake.NewId(), Label = "Home", Url = "/", PageId = homepage.Id, Order = 0, AltText = "Home Page" } },
                { 1, new NavigationBlock.NavigationBlockItem { Id = Snowflake.NewId(), Label = "About", Url = "/about", PageId = aboutPage.Id, Order = 1, AltText = "About Us" } },
                { 2, new NavigationBlock.NavigationBlockItem { Id = Snowflake.NewId(), Label = "Contact", Url = "/contact", PageId = contactPage.Id, Order = 2, AltText = "Contact Us" } },
                { 3, new NavigationBlock.NavigationBlockItem { Id = Snowflake.NewId(), Label = "Blog", Url = "/blog", PageId = blogListing.Id, Order = 3, AltText = "Blog and Field Notes" } },
                { 4, new NavigationBlock.NavigationBlockItem { Id = Snowflake.NewId(), Label = "Docs", Url = "/docs", PageId = rootDoc.Id, Order = 4, AltText = "Documentation" } }
            }
        };
        session.Store(mainNav);

        // Store pages and their blocks
        foreach (var block in homepageBlocks) session.Store(block);
        await pageContentService.SaveAsync(homepage, cancellationToken);

        foreach (var block in blogListingBlocks) session.Store(block);
        await pageContentService.SaveAsync(blogListing, cancellationToken);

        foreach (var block in aboutBlocks) session.Store(block);
        await pageContentService.SaveAsync(aboutPage, cancellationToken);

        foreach (var block in contactBlocks) session.Store(block);
        await pageContentService.SaveAsync(contactPage, cancellationToken);
        
        foreach (var doc in docs)
        {
            session.Store(doc);
        }

        // Build starter blog content (posts and tags)
        var (posts, tags) = BuildStarterBlogContent(request, staticPhotosClient);

        // Store tags first
        foreach (var tag in tags)
        {
            session.Store(tag);
        }

        // Save blog posts (blocks are stored inline in Content)
        foreach (var post in posts)
        {
            await blogPostContentService.SaveAsync(post, cancellationToken);
        }
    }

    private async Task SaveModuleStateAsync(CancellationToken cancellationToken)
    {
        var descriptors = await moduleDiscoveryService.DiscoverAsync(cancellationToken);
        var moduleStates = descriptors.Select(d => ModuleStateDocument.FromDescriptor(d, isBuiltIn: true));
        await moduleStateStore.SaveAllAsync(moduleStates, cancellationToken);
    }

    private static (PageDocument Page, List<BlockBase> Blocks) BuildHomepage(SeedDatabaseRequest request)
    {
        var headingBlock = new HeadingBlock
        {
            Id = Snowflake.NewId(),
            Level = 1,
            Text = Normalize(request.HomepageTitle),
            Order = 0
        };
        var bodyBlock = new RichTextBlock
        {
            Id = Snowflake.NewId(),
            Content = @"
                <style>
                    .no-scrollbar::-webkit-scrollbar { display: none; }
                    .no-scrollbar { -ms-overflow-style: none; scrollbar-width: none; }
                </style>
                <div class='max-w-4xl'>
                    <p class='text-xl leading-relaxed text-slate-700 mb-10'>
                        <strong>Aero CMS</strong> is a high-performance content platform designed for the next generation of web experience. 
                        Engineered with a relentless focus on efficiency, our ultimate goal is full <strong>Native AOT</strong> compatibility—delivering 
                        blindingly fast startup times and a minimal memory footprint.
                    </p>

                    <div class='grid grid-cols-1 md:grid-cols-2 gap-12 mb-16'>
                        <div class='space-y-4'>
                            <h3 class='text-lg font-bold text-slate-900 flex items-center gap-2'>
                                <span class='h-1 w-6 bg-indigo-600 rounded-full'></span>
                                The Power Core
                            </h3>
                            <p class='text-slate-600 leading-relaxed font-medium'>
                                Built on <strong>.NET 10</strong>, <strong>Marten</strong>, and <strong>PostgreSQL</strong>, we provide a sophisticated 
                                document-database experience with the reliability of a relational backend. <strong>Wolverine</strong> and 
                                <strong>LavinMQ</strong> handle our high-performance messaging, while <strong>S3 compatible storage</strong> 
                                ensures your assets are served globally at scale.
                            </p>
                        </div>
                        <div class='space-y-4'>
                            <h3 class='text-lg font-bold text-slate-900 flex items-center gap-2'>
                                <span class='h-1 w-6 bg-violet-600 rounded-full'></span>
                                Modern Frontend
                            </h3>
                            <p class='text-slate-600 leading-relaxed font-medium'>
                                We embrace the hypermedia revolution with <strong>HTMX</strong> and <strong>Alpine.js</strong>, supplemented by 
                                <strong>Lit</strong> and <strong>Preact</strong> for standard-based components. The entire ecosystem is 
                                <strong>.NET Aspire</strong> compatible and managed via powerful <strong>.NET MAUI</strong> clients.
                            </p>
                        </div>
                    </div>

                    <div class='mt-24 relative left-1/2 right-1/2 -ml-[50vw] -mr-[50vw] w-screen bg-white py-16 border-y border-slate-100'>
                        <div class='max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center'>
                            <h2 class='text-2xl font-black text-slate-900 uppercase tracking-widest mb-12'>Tech we use:</h2>
                            <div class='flex overflow-x-auto gap-16 pb-4 items-center no-scrollbar justify-center px-4'>
                                <img src='/img/dotnet-logo.svg' alt='DotNet' class='h-12 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/csharp.DJ9MidBD_1dalL.svg' alt='C#' class='h-12 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/postgresql.webp' alt='PostgreSQL' class='h-12 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/htmx-logo.png' alt='HTMX' class='h-8 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/typescript.C9-blvjE_1dalL.svg' alt='TypeScript' class='h-12 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/preact-logo.svg' alt='Preact' class='h-14 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/lavinmq.png' alt='LavinMQ' class='h-14 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/aspire.png' alt='Aspire' class='h-14 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/maui-icon.oIIgefok_ZfsSNl.webp' alt='MAUI' class='h-12 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                                <img src='/img/hydro_logo_s3.svg' alt='S3' class='h-12 w-auto transition-transform duration-500 hover:scale-110 drop-shadow-md' />
                            </div>
                        </div>
                    </div>
                </div>",
            Order = 1
        };

        return (
            new PageDocument
            {
                Id = Snowflake.NewId(),
                Kind = PageKind.Homepage,
                Slug = "/",
                Title = Normalize(request.HomepageTitle),
                Summary = $"A high-performance, block-based content platform built for scale. Experience the next generation of web management with {Normalize(request.SiteName)}.",
                SeoTitle = $"{Normalize(request.HomepageTitle)} | {Normalize(request.SiteName)}",
                SeoDescription = $"Welcome to {Normalize(request.SiteName)}. A modern CMS built on .NET 10, Marten, and Microsoft Orleans.",
                HeaderImageUrl = "/assets/hero-01.svg",
                LayoutRegions =
                [
                    new LayoutRegion
                    {
                        Name = "MainContent",
                        Order = 0,
                        Columns =
                        [
                            new LayoutColumn
                            {
                                Width = 12,
                                Order = 0,
                                Blocks =
                                [
                                    new BlockPlacement { BlockId = headingBlock.Id, BlockType = headingBlock.BlockType, Order = 0 },
                                    new BlockPlacement { BlockId = bodyBlock.Id, BlockType = bodyBlock.BlockType, Order = 1 }
                                ]
                            }
                        ]
                    }
                ],
                PublicationState = ContentPublicationState.Published
            },
            new List<BlockBase> { headingBlock, bodyBlock }
        );
    }

    private static (PageDocument Page, List<BlockBase> Blocks) BuildBlogListingPage(SeedDatabaseRequest request)
    {
        var headingBlock = new HeadingBlock
        {
            Id = Snowflake.NewId(),
            Level = 1,
            Text = Normalize(request.BlogName),
            Order = 0
        };
        var bodyBlock = new RichTextBlock
        {
            Id = Snowflake.NewId(),
            Content = "<p>Ten example posts are already published so the site is usable right away.</p>",
            Order = 1
        };

        return (
            new PageDocument
            {
                Id = Snowflake.NewId(),
                Kind = PageKind.BlogListing,
                Slug = "blog",
                Title = Normalize(request.BlogName),
                Summary = $"Updates and field notes from {Normalize(request.SiteName)}.",
                SeoTitle = $"{Normalize(request.BlogName)} | {Normalize(request.SiteName)}",
                SeoDescription = $"Read the latest posts from {Normalize(request.SiteName)}.",
                LayoutRegions =
                [
                    new LayoutRegion
                    {
                        Name = "MainContent",
                        Order = 0,
                        Columns =
                        [
                            new LayoutColumn
                            {
                                Width = 12,
                                Order = 0,
                                Blocks =
                                [
                                    new BlockPlacement { BlockId = headingBlock.Id, BlockType = headingBlock.BlockType, Order = 0 },
                                    new BlockPlacement { BlockId = bodyBlock.Id, BlockType = bodyBlock.BlockType, Order = 1 }
                                ]
                            }
                        ]
                    }
                ],
                PublicationState = ContentPublicationState.Published
            },
            new List<BlockBase> { headingBlock, bodyBlock }
        );
    }

    private static (PageDocument Page, List<BlockBase> Blocks) BuildAboutPage()
    {
        var headingBlock = new HeadingBlock
        {
            Id = Snowflake.NewId(),
            Level = 1,
            Text = "About Us",
            Order = 0
        };
        var bodyBlock = new RichTextBlock
        {
            Id = Snowflake.NewId(),
            Content = "<p class='text-lg leading-relaxed text-slate-700 mb-6'>We believe that content management should be intuitive, performant, and extensible. Our team is dedicated to building tools that empower creators to share their vision without technical friction.</p>" +
                      "<p class='text-lg leading-relaxed text-slate-700'>Founded on the principles of clarity and engineering excellence, Aero CMS is the culmination of years of experience in distributed systems and modern web architecture.</p>",
            Order = 1
        };

        return (
            new PageDocument
            {
                Id = Snowflake.NewId(),
                Kind = PageKind.Standard,
                Slug = "about",
                Title = "About Us",
                Summary = "Learn more about our mission and the team behind the platform.",
                SeoTitle = "About Us | Aero CMS",
                SeoDescription = "Discover our story, mission, and commitment to building great digital experiences.",
                LayoutRegions =
                [
                    new LayoutRegion
                    {
                        Name = "MainContent",
                        Order = 0,
                        Columns =
                        [
                            new LayoutColumn
                            {
                                Width = 12,
                                Order = 0,
                                Blocks =
                                [
                                    new BlockPlacement { BlockId = headingBlock.Id, BlockType = headingBlock.BlockType, Order = 0 },
                                    new BlockPlacement { BlockId = bodyBlock.Id, BlockType = bodyBlock.BlockType, Order = 1 }
                                ]
                            }
                        ]
                    }
                ],
                PublicationState = ContentPublicationState.Published
            },
            new List<BlockBase> { headingBlock, bodyBlock }
        );
    }

    private static (PageDocument Page, List<BlockBase> Blocks) BuildContactPage()
    {
        var headingBlock = new HeadingBlock
        {
            Id = Snowflake.NewId(),
            Level = 1,
            Text = "Contact Us",
            Order = 0
        };
        var bodyBlock = new RichTextBlock
        {
            Id = Snowflake.NewId(),
            Content = "<p class='text-lg leading-relaxed text-slate-700 mb-8'>Have a question or looking to collaborate? We'd love to hear from you. Our team typically responds within 24 hours.</p>",
            Order = 1
        };
        var ctaBlock = new CtaBlock
        {
            Id = Snowflake.NewId(),
            Text = "Send Us a Message",
            Url = "mailto:hello@example.com",
            Style = "primary",
            Order = 2
        };

        return (
            new PageDocument
            {
                Id = Snowflake.NewId(),
                Kind = PageKind.Standard,
                Slug = "contact",
                Title = "Contact Us",
                Summary = "Get in touch with our team.",
                SeoTitle = "Contact Us | Aero CMS",
                SeoDescription = "Have questions? We'd love to hear from you. Send us a message today.",
                LayoutRegions =
                [
                    new LayoutRegion
                    {
                        Name = "MainContent",
                        Order = 0,
                        Columns =
                        [
                            new LayoutColumn
                            {
                                Width = 12,
                                Order = 0,
                                Blocks =
                                [
                                    new BlockPlacement { BlockId = headingBlock.Id, BlockType = headingBlock.BlockType, Order = 0 },
                                    new BlockPlacement { BlockId = bodyBlock.Id, BlockType = bodyBlock.BlockType, Order = 1 },
                                    new BlockPlacement { BlockId = ctaBlock.Id, BlockType = ctaBlock.BlockType, Order = 2 }
                                ]
                            }
                        ]
                    }
                ],
                PublicationState = ContentPublicationState.Published
            },
            new List<BlockBase> { headingBlock, bodyBlock, ctaBlock }
        );
    }

    private static (IReadOnlyList<BlogPostDocument> Posts, IReadOnlyList<Tag> Tags) BuildStarterBlogContent(SeedDatabaseRequest request, IStaticPhotosClient staticPhotosClient)
    {
        var random = new Random();
        var tags = CreateTags();
        var tagMap = tags.ToDictionary(t => t.Name, t => t.Id);

        var posts = new List<BlogPostDocument>
        {
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/welcome-to-our-new-platform",
                title: "Welcome to Our New Platform",
                excerpt: "Launching a better way to share updates and connect with our community.",
                headingText: "Welcome to Our New Platform",
                bodyHtml: $"<p>We're thrilled to unveil our new digital home. This platform marks a significant step forward in how we communicate, share, and engage with you—our community.</p>" +
                          "<p>Built from the ground up with modern technology, this site represents our commitment to speed, accessibility, and user experience. Every pixel has been crafted with care, every feature designed with purpose.</p>" +
                          "<p>As you explore, you'll find our blog at the heart of this platform. This is where we'll share insights, announce updates, and tell the stories behind our work.</p>",
                tagIds: [tagMap["announcements"], tagMap["community"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("technology"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/behind-the-scenes-building-a-content-management-system",
                title: "Behind the Scenes: Building a Content Management System",
                excerpt: "A deep dive into the technical decisions that power our content platform.",
                headingText: "Behind the Scenes: Building a Content Management System",
                bodyHtml: "<p>Creating a CMS from scratch is both exhilarating and challenging. In this post, we're pulling back the curtain on the architectural decisions that shape our platform.</p>" +
                          "<p>We chose .NET 10 for its performance and robust ecosystem. MartenDB provides the document storage layer, giving us flexibility in our schema while maintaining query performance. The block-based content model allows for rich, modular layouts.</p>" +
                          "<p>The result is a system that's fast, flexible, and fun to use. Stay tuned for more technical deep dives.</p>",
                quoteText: "The best systems are those that disappear, letting creators focus on what matters—creating.",
                quoteAuthor: "Our Team",
                tagIds: [tagMap["architecture"], tagMap["cms"], tagMap[".net"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("technology"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/design-principles-for-modern-web-platforms",
                title: "Design Principles for Modern Web Platforms",
                excerpt: "How we approach design to create interfaces that feel natural and intuitive.",
                headingText: "Design Principles for Modern Web Platforms",
                bodyHtml: "<p>Good design is invisible. It works so well that users never notice the effort behind it. That's the standard we hold ourselves to.</p>" +
                          "<p>Our design philosophy centers on three pillars: clarity, speed, and delightful interactions. Every component we build must serve a purpose, load instantly, and feel natural to use.</p>" +
                          "<p>We believe in progressive enhancement—starting with a solid, accessible foundation and layering on polished experiences for capable browsers. This ensures everyone gets a great experience, regardless of device or connection.</p>",
                tagIds: [tagMap["design"], tagMap["ux"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("workspace"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/choosing-orleans-for-distributed-systems",
                title: "Choosing Orleans for Distributed Systems",
                excerpt: "Why we selected Orleans as our service framework and what we've learned.",
                headingText: "Choosing Orleans for Distributed Systems",
                bodyHtml: "<p>When architecting a platform that needs to scale, the choice of service framework is critical. After evaluating several options, we chose Microsoft Orleans for its balance of simplicity and power.</p>" +
                          "<p>Orleans brings the actor model to .NET in a way that feels natural. Grains provide a clean mental model for stateful services, while the runtime handles distribution, persistence, and scaling concerns.</p>" +
                          "<p>What sold us most was the developer experience. The programming model is intuitive, the debugging story is solid, and the documentation is excellent. After months of building, we haven't looked back.</p>",
                quoteText: "Orleans lets us think about business logic, not infrastructure. That's exactly what we needed.",
                tagIds: [tagMap["orleans"], tagMap["distributed-systems"], tagMap[".net"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("architecture"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/content-strategy-blogging-best-practices",
                title: "Content Strategy: Blogging Best Practices",
                excerpt: "Tips for maintaining a consistent publishing cadence and quality content.",
                headingText: "Content Strategy: Blogging Best Practices",
                bodyHtml: "<p>Starting a blog is easy. Maintaining one is hard. Here's what we've learned about building a sustainable content practice.</p>" +
                          "<p>First, quality beats quantity. We'd rather publish one excellent article than three mediocre ones. Each post should add genuine value—whether that's solving a problem, sharing insight, or telling a compelling story.</p>" +
                          "<p>Second, consistency builds trust. When readers know they can expect content on a regular schedule, they become loyal followers. We publish weekly, every Tuesday morning.</p>" +
                          "<p>Finally, engage with your audience. Respond to comments, answer questions, and acknowledge feedback. The best blogs are conversations, not monologues.</p>",
                tagIds: [tagMap["content-strategy"], tagMap["blogging"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("office"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/scaling-postgres-for-high-traffic",
                title: "Scaling Postgres for High Traffic",
                excerpt: "Lessons learned from optimizing our database layer for performance.",
                headingText: "Scaling Postgres for High Traffic",
                bodyHtml: "<p>PostgreSQL is remarkably capable, but pushing it to its limits requires thoughtfulness. Here's how we handle traffic spikes without breaking a sweat.</p>" +
                          "<p>Indexing is everything. Every query was analyzed and optimized. We use covering indexes for read-heavy paths, partial indexes for filtered queries, and GIN indexes for full-text search. The difference in performance is night and day.</p>" +
                          "<p>Connection pooling is essential. With Marten's pooling built-in, we reuse connections efficiently, avoiding the overhead of establishing new connections for each request.</p>" +
                          "<p>And always, always monitor. Query stats, connection counts, cache hit ratios—know your system's vital signs before problems arise.</p>",
                quoteText: "Premature optimization is the root of all evil. But so is ignoring performance until it bites you.",
                tagIds: [tagMap["postgresql"], tagMap["performance"], tagMap["database"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("technology"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/embracing-blazor-and-htmx",
                title: "Embracing Blazor and HTMX for Interactive UIs",
                excerpt: "How we combine server-side rendering with progressive enhancement.",
                headingText: "Embracing Blazor and HTMX for Interactive UIs",
                bodyHtml: "<p>The web development landscape is fractured between full-page reload purists and SPA enthusiasts. We found a middle ground that gives us the best of both worlds.</p>" +
                          "<p>Blazor provides rich, interactive components in C#. We use Radzen's component library for rapid development of complex UI elements—data grids, editors, dialogs. Everything works without writing JavaScript.</p>" +
                          "<p>HTMX adds the dynamic touch. With a few attributes, we enable seamless partial page updates, infinite scroll, and real-time interactions. The pattern is simple: server renders HTML, HTMX swaps it in. No client-side routing, no hydration complexity.</p>" +
                          "<p>The result is a site that loads fast, works without JavaScript, yet feels modern and responsive. That's the sweet spot.</p>",
                tagIds: [tagMap["blazor"], tagMap["htmx"], tagMap["frontend"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("workspace"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/open-telemetry-observability-at-scale",
                title: "OpenTelemetry: Observability at Scale",
                excerpt: "Implementing distributed tracing and metrics in our platform.",
                headingText: "OpenTelemetry: Observability at Scale",
                bodyHtml: "<p>When systems grow complex, intuition fails. You need data. That's where observability comes in, and OpenTelemetry is our tool of choice.</p>" +
                          "<p>We instrument everything with OpenTelemetry: requests, database calls, cache operations, message processing. Using Serilog as our logging foundation and OpenObserve for storage and visualization, we have complete visibility into system behavior.</p>" +
                          "<p>Traces let us follow requests across service boundaries, finding where latency bubbles up. Metrics show trends over time—error rates, response times, throughput. Logs provide the detail when something goes wrong.</p>" +
                          "<p>The investment pays dividends every incident. Instead of guessing, we know exactly what happened and where.</p>",
                quoteText: "Without observability, you're flying blind. With it, you can debug with confidence.",
                tagIds: [tagMap["observability"], tagMap["opentelemetry"], tagMap["monitoring"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("science"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/getting-started-with-aero-cms",
                title: "Getting Started with Aero CMS",
                excerpt: $"Use {Normalize(request.SiteName)} to publish your first update in minutes.",
                headingText: "Getting Started with Aero CMS",
                bodyHtml: $"<p>Your site is live with a homepage and blog. Use this starter post as the baseline for your first editorial update in {Normalize(request.SiteName)}.</p>" +
                          "<p>The platform is designed to be intuitive. Create pages, arrange blocks, publish content—all without touching code. But if you need to extend functionality, the architecture is open and extensible.</p>" +
                          "<p>Browse the admin panel to explore what's possible. Add new pages, create blog posts, arrange content blocks, customize the design. This is just the beginning.</p>",
                tagIds: [tagMap["cms"], tagMap["tutorial"], tagMap["guide"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("education"),
                likes: random.Next(1, 1001)),
            CreatePost(
                id: Snowflake.NewId(),
                slug: "blog/the-future-of-content-management",
                title: "The Future of Content Management",
                excerpt: "Where we see the CMS space heading and what it means for content creators.",
                headingText: "The Future of Content Management",
                bodyHtml: "<p>The CMS landscape is evolving. Traditional monoliths give way to composable architectures. Proprietary formats yield to open standards. We're excited about where it's heading.</p>" +
                          "<p>Block-based content models are becoming the norm. Instead of rigid templates, editors compose with reusable components. This flexibility unlocks creativity while maintaining consistency.</p>" +
                          "<p>Headless is hot, but we're skeptical of the one-size-fits-all pitch. Most teams need a cohesive system, not a puzzle of separate products. We believe in integrated solutions that just work.</p>" +
                          "<p>The future is fast, accessible, and focused on the creator experience. That's the future we're building toward.</p>",
                tagIds: [tagMap["future"], tagMap["cms"], tagMap["trends"]],
                imageUrl: staticPhotosClient.GetPhotoUrl("technology"),
                likes: random.Next(1, 1001))
        };

        // Add 20 more posts
        var techTopics = new[]
        {
            "Cloud Native Architecture", "Microservices Patterns", "WebAssembly and Blazor", "AI Integration in CMS",
            "Performance Tuning .NET 10", "Durable Functions Workflows", "Message Queues with Wolverine", "GraphQL API Design",
            "Modern CSS with Tailwind", "Responsive Design Best Practices", "Unit Testing with TUnit", "Infrastructure as Code",
            "Identity and Access Management", "Distributed Caching Strategies", "Real-time Apps with SignalR", "SEO Optimization Guide",
            "Static Site Generation", "Dynamic Block Rendering", "Headless CMS Advantages", "The Creator Economy Tools"
        };

        var allTagIds = tags.Select(t => t.Id).ToList();

        for (int i = 0; i < 20; i++)
        {
            var topic = techTopics[i];
            var slugTopic = topic.ToLowerInvariant().Replace(' ', '-');
            
            posts.Add(CreatePost(
                id: Snowflake.NewId(),
                slug: $"blog/{slugTopic}",
                title: topic,
                excerpt: $"Exploring the nuances of {topic} in the context of modern enterprise applications.",
                headingText: topic,
                bodyHtml: $"<p>{topic} is a crucial area of modern software development. In this deep dive, we examine the core principles and how they apply to building high-performance systems.</p>" +
                          "<p>As we move towards more distributed and resilient architectures, understanding the underlying patterns becomes even more important.</p>",
                tagIds: allTagIds.OrderBy(_ => random.Next()).Take(3).ToList(),
                imageUrl: staticPhotosClient.GetPhotoUrl("technology"),
                likes: random.Next(1, 500)));
        }

        return (posts, tags);
    }

    private static BlogPostDocument CreatePost(long id, string slug, string title, string excerpt, string headingText, string bodyHtml, string? quoteText = null, string? quoteAuthor = null, List<long>? tagIds = null, string? imageUrl = null, int likes = 0)
    {
        var blocks = new List<BlockBase>
        {
            new HeadingBlock { Id = Snowflake.NewId(), Level = 1, Text = headingText, Order = 0 },
            new RichTextBlock { Id = Snowflake.NewId(), Content = bodyHtml, Order = 1 }
        };
        
        if (!string.IsNullOrEmpty(quoteText))
        {
            blocks.Add(new QuoteBlock { Id = Snowflake.NewId(), Content = quoteText, Author = quoteAuthor, Order = (short)(blocks.Count) });
        }

        return new BlogPostDocument
        {
            Id = id,
            Slug = slug,
            Title = title,
            Excerpt = excerpt,
            SeoTitle = title,
            SeoDescription = excerpt,
            Content = blocks,
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            TagIds = tagIds ?? [],
            ImageUrl = imageUrl,
            Likes = likes
        };
    }

    private static string Normalize(string value)
        => value.Trim();

    private static List<Tag> CreateTags()
    {
        var tagNames = new[]
        {
            "announcements", "community", "architecture", "cms", ".net", "design", "ux", "orleans",
            "distributed-systems", "content-strategy", "blogging", "postgresql", "performance",
            "database", "blazor", "htmx", "frontend", "observability", "opentelemetry", "monitoring",
            "tutorial", "guide", "future", "trends"
        };
        return tagNames.Select(name => new Tag
        {
            Id = Snowflake.NewId(),
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-')
        }).ToList();
    }

    private List<DocsPage> BuildStarterDocsContent()
    {
        var docs = new List<DocsPage>();
        
        // 1. Root Documentation Page
        var rootDoc = new DocsPage
        {
            Id = Snowflake.NewId(),
            Title = "Aero CMS Documentation",
            Slug = "docs",
            Summary = "Official developer documentation for Aero CMS—the high-performance, AOT-compatible content platform.",
            MarkdownContent = @"# Aero CMS Documentation

Welcome to the official developer documentation for **Aero CMS**. Use the guides below to explore our architecture and features.

## Getting Started
Learn how to install and configure Aero CMS for your next project.

## Advanced Guides
Deep dives into theming, localization, and custom module development.

## API Reference
Technical documentation for integrating with the Aero CMS core services.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0,
            HeaderImageUrl = staticPhotosClient.GetPhotoUrl("tech", "1920x1080"),
            SeoTitle = "Aero CMS Documentation - Knowledge Base",
            SeoDescription = "Learn how to build and extend Aero CMS with our comprehensive developer guides."
        };
        docs.Add(rootDoc);

        // 2. Getting Started Chapter
        var gettingStarted = new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = rootDoc.Id,
            Title = "Getting Started",
            Slug = "docs/getting-started",
            Summary = "Everything you need to know to get Aero CMS up and running.",
            MarkdownContent = "# Getting Started\n\nWelcome to Aero CMS! This chapter covers the basics of settting up your development environment. Aero CMS is designed to be lean and fast, leveraging the latest .NET features.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0
        };
        docs.Add(gettingStarted);

        // 3. Installation Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = gettingStarted.Id,
            Title = "Installation",
            Slug = "docs/getting-started/installation",
            Summary = "Step-by-step guide to installing Aero CMS via CLI or source.",
            MarkdownContent = "# Installation\n\nTo install Aero CMS, you can clone the repository directly or use our upcoming dotnet new templates.\n\n```bash\ngit clone https://github.com/microbian-systems/AeroCMS.git\ncd AeroCMS\ndotnet build\n```",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0
        });

        // 4. Configuration Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = gettingStarted.Id,
            Title = "Configuration",
            Slug = "docs/getting-started/configuration",
            Summary = "How to configure your database, caching, and storage providers.",
            MarkdownContent = "# Configuration\n\nAll configuration is handled through standard .NET configuration providers. The primary settings are located in `appsettings.json` and can be overridden by environment variables.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 1
        });

        // 5. Guides Chapter
        var guides = new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = rootDoc.Id,
            Title = "Guides",
            Slug = "docs/guides",
            Summary = "Practical tutorials for common tasks like theming and localization.",
            MarkdownContent = "# Guides\n\nOur guides are designed to help you solve real-world problems with Aero CMS. Whether you're building a simple blog or a complex enterprise portal, you'll find what you need here.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 1
        };
        docs.Add(guides);

        // 6. Theming Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = guides.Id,
            Title = "Theming",
            Slug = "docs/guides/theming",
            Summary = "Learn how to use Tailwind CSS and CSS variables to style your site.",
            MarkdownContent = "# Theming\n\nAero CMS uses a modern CSS utility approach. You can customize the look and feel by modifying the `tailwind.config.js` or providing custom global styles via the admin interface.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0
        });

        // 7. Localization Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = guides.Id,
            Title = "Localization",
            Slug = "docs/guides/localization",
            Summary = "Setting up multi-lingual sites and managing translations.",
            MarkdownContent = "# Localization\n\nInternationalization is built into the core. You can define multiple languages and provide translated versions of all your content, including pages, posts, and documentation.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 1
        });

        // 8. API Reference Chapter
        var api = new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = rootDoc.Id,
            Title = "API Reference",
            Slug = "docs/api",
            Summary = "Detailed technical specifications for the Aero CMS core API.",
            MarkdownContent = "# API Reference\n\nThe Aero CMS API provides programmatic access to all system functionality. This reference documentation covers the REST endpoints and the underlying C# service contracts.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 2
        };
        docs.Add(api);

        // 9. Authentication Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = api.Id,
            Title = "Authentication",
            Slug = "docs/api/authentication",
            Summary = "Securing your API requests with Bearer tokens and OAuth.",
            MarkdownContent = "# API Authentication\n\nAero CMS uses JWT-based authentication for its API. To make requests, you must obtain a token and include it in the `Authorization` header of your HTTP requests.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0
        });

        // 10. Content Management Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = api.Id,
            Title = "Content Mgmt",
            Slug = "docs/api/content-management",
            Summary = "Programmatically creating and updating pages and blocks.",
            MarkdownContent = "# Content Management Service\n\nThe `IContentService` is the primary interface for managing content entities. It provides methods for creating, retrieving, updating, and deleting documents across all modules.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 1
        });

        return docs;
    }
}
