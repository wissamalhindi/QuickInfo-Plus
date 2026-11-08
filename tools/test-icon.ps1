param(
    [string]$Path = (Join-Path $PSScriptRoot '..\src\QuickInfoPlus\Resources\icon.png')
)

$ErrorActionPreference = 'Stop'
$Path = [IO.Path]::GetFullPath($Path)
$failed = 0

function Report([bool]$ok, [string]$name, [string]$reason) {
    if ($ok) { Write-Output "PASS $name" }
    else { Write-Output "FAIL $name - $reason"; $script:failed++ }
}

if (-not (Test-Path -LiteralPath $Path)) {
    Report $false 'file exists' "not found: $Path"
    exit 1
}
Report $true 'file exists' ''

Add-Type -AssemblyName System.Drawing
$bitmap = [System.Drawing.Bitmap]::new($Path)
try {
    Report ($bitmap.Width -eq 128 -and $bitmap.Height -eq 128) 'size 128x128' "is $($bitmap.Width)x$($bitmap.Height)"
    if ($bitmap.Width -lt 128 -or $bitmap.Height -lt 128) { exit 1 }

    foreach ($corner in @(@(0, 0), @(127, 0), @(0, 127), @(127, 127))) {
        $pixel = $bitmap.GetPixel($corner[0], $corner[1])
        Report ($pixel.A -eq 0) "transparent corner ($($corner[0]),$($corner[1]))" "alpha is $($pixel.A)"
    }

    $colorChecks = @(
        @{ Name = 'signature bar';         X = 50;  Y = 27;  Color = '#6B3FD4' },
        @{ Name = 'left outline';          X = 10;  Y = 50;  Color = '#6B3FD4' },
        @{ Name = 'tooltip body';          X = 60;  Y = 84;  Color = '#FFFFFF' },
        @{ Name = 'code block background'; X = 60;  Y = 67;  Color = '#E6E8EE' },
        @{ Name = 'badge';                 X = 88;  Y = 104; Color = '#2EA043' },
        @{ Name = 'plus sign';             X = 100; Y = 98;  Color = '#FFFFFF' }
    )
    foreach ($check in $colorChecks) {
        $expected = [System.Drawing.ColorTranslator]::FromHtml($check.Color)
        $pixel = $bitmap.GetPixel($check.X, $check.Y)
        $ok = $pixel.A -eq 255 -and
            [Math]::Abs($pixel.R - $expected.R) -le 8 -and
            [Math]::Abs($pixel.G - $expected.G) -le 8 -and
            [Math]::Abs($pixel.B - $expected.B) -le 8
        $actual = '#{0:X2}{1:X2}{2:X2} alpha {3}' -f $pixel.R, $pixel.G, $pixel.B, $pixel.A
        Report $ok "$($check.Name) ($($check.X),$($check.Y)) is $($check.Color)" "is $actual"
    }
}
finally {
    $bitmap.Dispose()
}

if ($failed -gt 0) { exit 1 }
exit 0
