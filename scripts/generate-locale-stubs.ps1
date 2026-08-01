# Generates stub .resx files for NeoUI, Manager, and Setup resources
# across all supported locales. English .resx files must exist as templates.

param(
    [string]$SharedResourcesDir = "D:\proj\microbians\AeroCMS\src\Aero.Cms.Shared\Resources",
    [string]$SetupResourcesDir = "D:\proj\microbians\AeroCMS\src\Aero.Cms.Modules.Setup\Resources"
)

$locales = @(
    @{Name="es-MX";  Display="Spanish (Mexico)"},
    @{Name="ja";     Display="Japanese"},
    @{Name="zh-Hant"; Display="Chinese (Traditional)"},
    @{Name="zh-Hans"; Display="Chinese (Simplified)"},
    @{Name="hi-IN";  Display="Hindi (India)"},
    @{Name="ru";     Display="Russian"},
    @{Name="uk";     Display="Ukrainian"},
    @{Name="ko";     Display="Korean"},
    @{Name="it";     Display="Italian"},
    @{Name="de";     Display="German"},
    @{Name="fr";     Display="French"},
    @{Name="pt-BR";  Display="Portuguese (Brazil)"},
    @{Name="da";     Display="Danish"},
    @{Name="sv";     Display="Swedish"},
    @{Name="nl";     Display="Dutch"}
)

function New-ResxStub($baseName, $localeName, $outDir) {
    $fileName = if ($localeName) { "$baseName.$localeName.resx" } else { "$baseName.resx" }
    $fullPath = Join-Path $outDir $fileName

    if (Test-Path $fullPath -PathType Leaf) {
        Write-Host "  SKIP $fileName (exists)" -ForegroundColor DarkGray
        return
    }

    $stubContent = @"
<?xml version="1.0" encoding="utf-8"?>
<root>
  <!--
    $baseName resource file — $localeName
    Display name: $($locales | Where-Object { $_.Name -eq $localeName } | ForEach-Object { $_.Display } ?? $localeName)
    Translation status: NEEDS TRANSLATION
    Copy keys from the English .resx ($baseName.resx) and translate the values.
  -->
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <!-- TODO: Add translated <data> entries here. See $baseName.resx for reference keys. -->
</root>
"@
    Set-Content -Path $fullPath -Value $stubContent -Encoding UTF8
    Write-Host "  OK   $fileName" -ForegroundColor Green
}

Write-Host "`n=== NeoUiSharedResource stubs ===" -ForegroundColor Cyan
foreach ($loc in $locales) {
    New-ResxStub "Aero.Cms.Shared.Localization.NeoUiSharedResource" $loc.Name $SharedResourcesDir
}

Write-Host "`n=== ManagerResource stubs ===" -ForegroundColor Cyan
foreach ($loc in $locales) {
    New-ResxStub "Aero.Cms.Shared.Localization.ManagerResource" $loc.Name $SharedResourcesDir
}

Write-Host "`n=== SetupResource stubs ===" -ForegroundColor Cyan
foreach ($loc in $locales) {
    New-ResxStub "Aero.Cms.Modules.Setup.Areas.Setup.Pages.SetupResource" $loc.Name $SetupResourcesDir
}

Write-Host "`nDone! Created missing locale stubs." -ForegroundColor Green
