using Aero.Cms.Modules.Commerce.Orders.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

namespace Aero.Cms.Modules.Commerce;

/// <summary>
/// Represents a class for CommerceStartupFilter.
/// </summary>
public sealed class CommerceStartupFilter : IStartupFilter
{
        /// <summary>
    /// Configure method.
    /// </summary>
public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            using var scope = app.ApplicationServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            db.Database.Migrate();

            next(app);
        };
    }
}