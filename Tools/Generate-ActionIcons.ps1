param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\Textures\UI\ResearchQueue')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

# Draw on a 24-unit grid at high resolution, then downsample to transparent UI textures.
foreach ($name in 'MoveToTop', 'Snooze', 'Restore', 'VanillaTree', 'NodeResearch', 'ResearchQueue') {
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
        } elseif ($name -eq 'Restore') {
            # An undo arrow: a thick ring segment with a solid head on its upper end, pointing along the curve.
            $cx = 13.0; $cy = 12.5; $radius = 7.5; $thickness = 3.0
            $start = 250.0; $sweep = 290.0
            $outer = $radius + $thickness / 2; $inner = $radius - $thickness / 2
            $path.AddArc($cx - $outer, $cy - $outer, $outer * 2, $outer * 2, $start, $sweep)
            $path.AddArc($cx - $inner, $cy - $inner, $inner * 2, $inner * 2, $start + $sweep, -$sweep)
            $path.CloseFigure()
            # The head's base lies along the radius at the ring's end, so the ring's flat end meets it squarely.
            $angle = $start * [Math]::PI / 180
            $ux = [Math]::Cos($angle); $uy = [Math]::Sin($angle)
            $dx = $uy; $dy = -$ux
            $px = $cx + $radius * $ux; $py = $cy + $radius * $uy
            $half = 4.0; $length = 5.0
            $path.AddPolygon([Drawing.PointF[]]@(
                [Drawing.PointF]::new($px + $half * $ux, $py + $half * $uy),
                [Drawing.PointF]::new($px + $length * $dx, $py + $length * $dy),
                [Drawing.PointF]::new($px - $half * $ux, $py - $half * $uy)))
            $path.FillMode = [Drawing.Drawing2D.FillMode]::Winding
            $graphics.FillPath([Drawing.Brushes]::White, $path)
        } elseif ($name -eq 'VanillaTree') {
            # A research tree: one node above a bar branching to three, for switching to the vanilla tree.
            $white = [Drawing.Brushes]::White
            $graphics.FillRectangle($white, 9, 2, 6, 5)
            $graphics.FillRectangle($white, 11, 7, 2, 4)
            $graphics.FillRectangle($white, 3.5, 11, 17, 2)
            foreach ($x in 3.5, 11, 18.5) { $graphics.FillRectangle($white, $x, 13, 2, 3) }
            foreach ($x in 1.5, 9, 16.5) { $graphics.FillRectangle($white, $x, 16, 6, 5) }
        } elseif ($name -eq 'NodeResearch') {
            # A node graph: three linked nodes, for switching to Node Research's tree.
            $nodes = @(@(6, 6.5), @(18.5, 9), @(10, 18.5))
            foreach ($link in @(@(0, 1), @(1, 2), @(2, 0))) {
                $a = $nodes[$link[0]]; $b = $nodes[$link[1]]
                $lx = $b[0] - $a[0]; $ly = $b[1] - $a[1]; $len = [Math]::Sqrt($lx * $lx + $ly * $ly)
                $nx = -$ly / $len * 1.1; $ny = $lx / $len * 1.1
                # Each shape is filled on its own: overlapping shapes in one path would cut holes where they cross.
                $graphics.FillPolygon([Drawing.Brushes]::White, [Drawing.PointF[]]@(
                    [Drawing.PointF]::new($a[0] + $nx, $a[1] + $ny),
                    [Drawing.PointF]::new($b[0] + $nx, $b[1] + $ny),
                    [Drawing.PointF]::new($b[0] - $nx, $b[1] - $ny),
                    [Drawing.PointF]::new($a[0] - $nx, $a[1] - $ny)))
            }
            foreach ($node in $nodes) { $graphics.FillEllipse([Drawing.Brushes]::White, $node[0] - 3.5, $node[1] - 3.5, 7, 7) }
        } elseif ($name -eq 'ResearchQueue') {
            # A queue: three bulleted rows, for returning to Research Queue.
            $white = [Drawing.Brushes]::White
            $graphics.FillRectangle($white, 2, 3, 4, 4)
            $graphics.FillRectangle($white, 8, 3, 14, 4)
            $graphics.FillRectangle($white, 2, 10, 4, 4)
            $graphics.FillRectangle($white, 8, 10, 14, 4)
            $graphics.FillRectangle($white, 2, 17, 4, 4)
            $graphics.FillRectangle($white, 8, 17, 14, 4)
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
