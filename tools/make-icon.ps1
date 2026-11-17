# Draws GraveShift's icon (a crescent moon over a headstone, ember glow) and writes a multi-size .ico.
# Run once when the icon changes: .\tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\src\GraveShift\Assets\GraveShift.ico"
$sizes = 16, 24, 32, 48, 64, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $f = $s / 64.0
    # night disc
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 32, 26, 46))), 1 * $f, 1 * $f, 62 * $f, 62 * $f)
    # crescent moon: a warm disc with a night disc cut out of it
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 170, 90))), 30 * $f, 7 * $f, 24 * $f, 24 * $f)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 32, 26, 46))), 37 * $f, 4 * $f, 22 * $f, 22 * $f)
    # headstone: rounded top, flat bottom
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(14 * $f, 22 * $f, 24 * $f, 24 * $f, 180, 180)
    $path.AddLine(38 * $f, 34 * $f, 38 * $f, 54 * $f)
    $path.AddLine(38 * $f, 54 * $f, 14 * $f, 54 * $f)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 236, 230, 245))), $path)
    # ember ground line
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 110, 40))), 8 * $f, 52 * $f, 48 * $f, [math]::Max(1, 4 * $f))
    if ($s -ge 32) {
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 32, 26, 46)), ([math]::Max(1, 2.5 * $f))
        $g.DrawLine($pen, 26 * $f, 30 * $f, 26 * $f, 44 * $f)
        $g.DrawLine($pen, 20 * $f, 35 * $f, 32 * $f, 35 * $f)
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}
$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
Write-Host "Wrote $out"
