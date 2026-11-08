$ErrorActionPreference = 'Stop'

$resources = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\QuickInfoPlus\Resources'))
$svgPath = Join-Path $resources 'icon.svg'
$pngPath = Join-Path $resources 'icon.png'

$browsers = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe'),
    (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -Unique
if (-not $browsers) {
    Write-Error 'Neither Microsoft Edge nor Google Chrome was found.'
}

function Export-WithBrowser([string]$browser) {
    $profileDir = Join-Path ([IO.Path]::GetTempPath()) ("export-icon-" + [Guid]::NewGuid())
    $started = Get-Date
    try {
        $arguments = @(
            '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
            '--force-device-scale-factor=1', '--window-size=128,128',
            '--default-background-color=00000000',
            "--user-data-dir=`"$profileDir`"",
            "--screenshot=`"$pngPath`"",
            "`"$([Uri]::new($svgPath).AbsoluteUri)`""
        )
        Start-Process -FilePath $browser -ArgumentList $arguments -Wait -WindowStyle Hidden
    }
    finally {
        Remove-Item -LiteralPath $profileDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    return (Test-Path -LiteralPath $pngPath) -and (Get-Item -LiteralPath $pngPath).LastWriteTime -ge $started
}

# Some machines' Edge exits without taking the screenshot, so try each browser until one writes the PNG.
$exported = $false
foreach ($browser in $browsers) {
    if (Export-WithBrowser $browser) {
        Write-Output "Exported $pngPath with $browser"
        $exported = $true
        break
    }
    Write-Warning "$browser did not write the PNG."
}
if (-not $exported) {
    Write-Error "No browser wrote $pngPath."
}

& (Join-Path $PSScriptRoot 'test-icon.ps1') -Path $pngPath
exit $LASTEXITCODE
