# Regenerates the OCR fixtures: black printed English text on white. Usage: pwsh generate-fixtures.ps1
# Prints the drawn line rectangles (x y width height), which WindowsOcrEngineTests keep as constants.
Add-Type -AssemblyName System.Drawing

function New-Fixture([string] $Name, [string[]] $Lines) {
    $bmp = [System.Drawing.Bitmap]::new(640, 220)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::White)
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $font = [System.Drawing.Font]::new('Arial', 28, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::GenericTypographic
    $y = 40
    foreach ($line in $Lines) {
        $size = $g.MeasureString($line, $font, [System.Drawing.PointF]::new(0, 0), $format)
        $g.DrawString($line, $font, [System.Drawing.Brushes]::Black, 40, $y, $format)
        "$Name '$line': 40 $y $([int][math]::Ceiling($size.Width)) $([int][math]::Ceiling($size.Height))"
        $y += 80
    }
    $bmp.Save((Join-Path $PSScriptRoot "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
}

New-Fixture 'two-lines' @('Hello World', 'Second line of text')
New-Fixture 'blank' @()
