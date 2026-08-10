namespace Aero.Cms.ExternalHosts.FSharp

open System
open Aero.Cms.Hosting.Defaults
open Aero.Cms.Web.Bootstrap
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

type HostMarker = class end

module Program =
    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)

        builder
            .AddAeroCms(AeroCmsDefaultCatalog.Catalog)
            .WithSetupSettingsDirectory(builder.Environment.ContentRootPath)
            .RegisterHostAsync(typeof<HostMarker>.Assembly)
            .GetAwaiter()
            .GetResult()
        |> ignore

        let app = builder.Build()

        app.UseAeroCmsRouting() |> ignore
        app.UseAeroCmsSiteAndLocalization() |> ignore
        app.UseAuthentication() |> ignore
        app.UseRateLimiter() |> ignore
        app.UseAuthorization() |> ignore
        app.UseAeroCmsRequestPipeline() |> ignore
        app.UseAntiforgery() |> ignore

        app.MapGet(
            "/consumer/health",
            Func<IResult>(fun () -> Results.Ok("F# consumer is running.")))
        |> ignore

        app.MapAeroCms() |> ignore
        app.UseAeroCmsTerminalPipeline() |> ignore
        app.Run()
        0
