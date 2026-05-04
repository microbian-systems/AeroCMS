using Aero.Cms.Modules.Commerce.Orders.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

namespace Aero.Cms.Modules.Commerce;

public sealed class CommerceStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var db = app.ApplicationServices.GetRequiredService<CommerceDbContext>();
            db.Database.Migrate();

            next(app);
        };
    }
}