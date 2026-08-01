# Theme asset authoring

Aero CMS serves committed, browser-ready CSS. Trusted authors may use Tailwind
or SCSS, but neither compiler runs in a public request path.

Build or watch the Tailwind inputs without npm:

```powershell
pwsh ./eng/theme-assets/build-theme-assets.ps1
pwsh ./eng/theme-assets/build-theme-assets.ps1 -Mode Watch
```

The script downloads the official Tailwind CSS standalone CLI v4.3.3 release
asset for the current OS and architecture into the ignored `.tools` directory.
Every binary is checked against the corresponding SHA-256 value from the
official release before it is executed.

The Web stylesheet also uses vendored DaisyUI v5.7.9 build plugins from
`src/Aero.Cms.Web/Styles/vendor/daisyui/5.7.9`. The build fails before
compilation if either plugin is missing or its pinned SHA-256 digest differs.
These ESM files are build inputs only; browsers receive the committed generated
CSS, not DaisyUI JavaScript.

Regular builds consume the committed generated CSS and fail when it is missing
or older than an authoring/view source. Regenerate automatically during a Web
build with:

```powershell
dotnet build ./src/Aero.Cms.Web/Aero.Cms.Web.csproj -p:AeroBuildThemeAssets=true
```

The deployment-installed Aero Safe theme is authored as SCSS in the Theming
module. `AspNetCore.SassCompiler` compiles it during that module's build and
publish targets. No runtime Sass watcher or compiler service is registered.
