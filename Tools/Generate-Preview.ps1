# Draws About\Preview.png in the style of the other Research mods' previews, in this mod's purple, with a faint queue of cards.
param(
    [string]$Out = (Join-Path $PSScriptRoot '..\About\Preview.png'),
    [int]$Red = 167,
    [int]$Green = 118,
    [int]$Blue = 232
)
Add-Type -AssemblyName System.Drawing

# Layout measured from Research: Total's preview: 1280x720, bar 140x10 at (100,189), label cap height 59 from y 238, word cap height 178 from y 358.
$bmp = New-Object System.Drawing.Bitmap 1280, 720
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(21, 24, 29))
$accent = [System.Drawing.Color]::FromArgb($Red, $Green, $Blue)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $accent), 100, 189, 140, 10)

# Draws text as a path scaled so its ink is exactly the given cap height, top-left at (x, y), with extra letter spacing.
function Draw-Text($text, $family, $style, $x, $y, $capHeight, $spacing, $brush) {
    $fam = New-Object System.Drawing.FontFamily $family
    $probe = New-Object System.Drawing.Drawing2D.GraphicsPath
    $probe.AddString('H', $fam, [int]$style, 100, (New-Object System.Drawing.PointF 0, 0), [System.Drawing.StringFormat]::GenericTypographic)
    $scale = $capHeight / $probe.GetBounds().Height
    $cursor = 0.0
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.FillMode = [System.Drawing.Drawing2D.FillMode]::Winding
    if ($spacing -eq 0) { $path.AddString($text, $fam, [int]$style, 100 * $scale, (New-Object System.Drawing.PointF 0, 0), [System.Drawing.StringFormat]::GenericTypographic) } else {
    foreach ($ch in $text.ToCharArray()) {
        $glyph = New-Object System.Drawing.Drawing2D.GraphicsPath
        $glyph.AddString([string]$ch, $fam, [int]$style, 100 * $scale, (New-Object System.Drawing.PointF $cursor, 0), [System.Drawing.StringFormat]::GenericTypographic)
        $path.AddPath($glyph, $false)
        $size = $g.MeasureString([string]$ch, (New-Object System.Drawing.Font $fam, ([single](100 * $scale)), ([System.Drawing.FontStyle]$style), ([System.Drawing.GraphicsUnit]::Pixel)), [System.Drawing.PointF]::Empty, [System.Drawing.StringFormat]::GenericTypographic)
        $cursor += $size.Width + $spacing
    }
    }
    $bounds = $path.GetBounds()
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate($x - $bounds.X, $y - $bounds.Y)
    $path.Transform($m)
    $g.FillPath($brush, $path)
}

Draw-Text 'RESEARCH' 'Bahnschrift' 0 107 238 59 22 (New-Object System.Drawing.SolidBrush $accent)
Draw-Text 'QUEUE' 'Bahnschrift SemiBold' 0 98 358 178 0 ([System.Drawing.Brushes]::White)

# A faint queue on the right: five stacked cards, the first highlighted in the accent like the current project.
for ($i = 0; $i -lt 5; $i++) {
    $y = 170 + $i * 78
    $alpha = 90 - $i * 14
    $fill = if ($i -eq 0) { [System.Drawing.Color]::FromArgb(70, $Red, $Green, $Blue) } else { [System.Drawing.Color]::FromArgb([Math]::Max(18, $alpha / 3), 255, 255, 255) }
    $line = if ($i -eq 0) { [System.Drawing.Color]::FromArgb(200, $Red, $Green, $Blue) } else { [System.Drawing.Color]::FromArgb($alpha, 255, 255, 255) }
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $fill), 920, $y, 300, 60)
    $g.DrawRectangle((New-Object System.Drawing.Pen $line, 2), 920, $y, 300, 60)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $line), 936, $y + 16, 28, 28)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $line), 980, $y + 24, 130 - $i * 12, 12)
}

$bmp.Save([IO.Path]::GetFullPath($Out), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
