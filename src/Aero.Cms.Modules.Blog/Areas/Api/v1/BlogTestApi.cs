using Aero.Auth.Services;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Abstractions.Services;
using Aero.Models.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Blog.Areas.Api.v1;

public static class BlogTestApi
{
    public static void MapBlogTestApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}test")
            .WithTags("Headless - Bearer Auth");

        group.MapGet("/hell-world", () => "Hello World")
            .WithName("CreateHeadlessToken");

        //group.MapPost("/refresh", RefreshToken)
        //    .WithName("RefreshHeadlessToken");
    }


}