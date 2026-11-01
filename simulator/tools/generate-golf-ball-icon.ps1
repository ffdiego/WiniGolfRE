Add-Type -AssemblyName System.Drawing

$outputDirectory = Join-Path $PSScriptRoot "../assets/icons"
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

$size = 256
$bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.Clear([System.Drawing.Color]::Transparent)

$sphere = [System.Drawing.Drawing2D.GraphicsPath]::new()
$sphere.AddEllipse(22, 22, 212, 212)
$fill = [System.Drawing.Drawing2D.PathGradientBrush]::new($sphere)
$fill.CenterPoint = [System.Drawing.PointF]::new(82, 72)
$fill.CenterColor = [System.Drawing.Color]::FromArgb(255, 255, 255, 249)
$fill.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 186, 207, 197))
$graphics.FillPath($fill, $sphere)

$graphics.SetClip($sphere)
$spacing = 25
for ($row = -4; $row -le 4; $row++) {
    for ($column = -4; $column -le 4; $column++) {
        $x = 128 + $column * $spacing + $(if (($row % 2) -eq 0) { 0 } else { 12 })
        $y = 128 + $row * $spacing
        $dx = $x - 128
        $dy = $y - 128
        if (($dx * $dx + $dy * $dy) -gt (82 * 82)) { continue }

        $shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(75, 40, 73, 64))
        $well = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(65, 104, 132, 120))
        $highlight = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(170, 255, 255, 247), 1.2)
        $graphics.FillEllipse($shadow, $x - 6.2, $y - 5.8, 12.4, 12.4)
        $graphics.FillEllipse($well, $x - 4.2, $y - 4.2, 8.4, 8.4)
        $graphics.DrawArc($highlight, $x - 4.2, $y - 4.2, 8.4, 8.4, 205, 120)
        $shadow.Dispose(); $well.Dispose(); $highlight.Dispose()
    }
}
$graphics.ResetClip()

$outline = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 35, 75, 63), 5)
$graphics.DrawEllipse($outline, 22, 22, 212, 212)
$outline.Dispose()

$pngPath = Join-Path $outputDirectory "winigolf-ball.png"
$icoPath = Join-Path $outputDirectory "winigolf-ball.ico"
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$handle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($handle)
$stream = [System.IO.File]::Create($icoPath)
try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose() }

$fill.Dispose(); $sphere.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
