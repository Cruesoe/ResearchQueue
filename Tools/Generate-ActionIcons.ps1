param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\Textures\UI\ResearchQueue')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

# Draw on a 24-unit grid at high resolution, then downsample to transparent UI textures.
foreach ($name in 'MoveToTop', 'Snooze') {
    $canvas = [Drawing.Bitmap]::new(768, 768)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(32, 32)
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    try {
        if ($name -eq 'MoveToTop') {
            $graphics.FillRectangle([Drawing.Brushes]::White, 2, 2, 20, 2.5)
            $path.AddPolygon([Drawing.PointF[]]@(
                [Drawing.PointF]::new(12, 6.5),
                [Drawing.PointF]::new(20, 14.5),
                [Drawing.PointF]::new(18, 16.5),
                [Drawing.PointF]::new(13.5, 12),
                [Drawing.PointF]::new(13.5, 22),
                [Drawing.PointF]::new(10.5, 22),
                [Drawing.PointF]::new(10.5, 12),
                [Drawing.PointF]::new(6, 16.5),
                [Drawing.PointF]::new(4, 14.5)))
            $graphics.FillPath([Drawing.Brushes]::White, $path)
        } else {
            # Explicit letter outlines avoid the spikes made by mitered diagonal strokes.
            foreach ($letter in @(@(2, 13, 7, 8), @(10.5, 8.5, 6, 7), @(18, 4, 5, 6))) {
                $x, $y, $w, $h = $letter
                $t = 1.8
                $path.Reset()
                $path.AddPolygon([Drawing.PointF[]]@(
                    [Drawing.PointF]::new($x, $y),
                    [Drawing.PointF]::new($x + $w, $y),
                    [Drawing.PointF]::new($x + $w, $y + $t),
                    [Drawing.PointF]::new($x + 2.5, $y + $h - $t),
                    [Drawing.PointF]::new($x + $w, $y + $h - $t),
                    [Drawing.PointF]::new($x + $w, $y + $h),
                    [Drawing.PointF]::new($x, $y + $h),
                    [Drawing.PointF]::new($x, $y + $h - $t),
                    [Drawing.PointF]::new($x + $w - 2.5, $y + $t),
                    [Drawing.PointF]::new($x, $y + $t)))
                $graphics.FillPath([Drawing.Brushes]::White, $path)
            }
        }
        $texture = [Drawing.Bitmap]::new(256, 256)
        $output = [Drawing.Graphics]::FromImage($texture)
        try {
            $output.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $output.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $output.DrawImage($canvas, 0, 0, 256, 256)
            for ($py = 0; $py -lt 256; $py++) {
                for ($px = 0; $px -lt 256; $px++) {
                    $alpha = $texture.GetPixel($px, $py).A
                    $texture.SetPixel($px, $py, [Drawing.Color]::FromArgb($alpha, 255, 255, 255))
                }
            }
            $texture.Save((Join-Path $OutputDirectory "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
        } finally {
            $output.Dispose()
            $texture.Dispose()
        }
    } finally {
        $path.Dispose()
        $graphics.Dispose()
        $canvas.Dispose()
    }
}
