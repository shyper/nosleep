Add-Type -AssemblyName System.Drawing

function Create-HighVisibilityIcon($icoPath) {
    $sizes = @(16, 32, 48)
    $dibBuffers = @()

    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        # Scale factors
        $scale = $size / 16.0

        # Background badge: Bright Indigo / Royal Blue Circle (#4f46e5 / #3b82f6)
        $pad = [Math]::Max(0.5, $size * 0.04)
        $diameter = $size - (2.0 * $pad)
        $brushBg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 59, 130, 246)) # Vibrant Blue
        $penBorder = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 255, 255), [Math]::Max(1.0, $size * 0.08)) # Crisp White Border
        
        $g.FillEllipse($brushBg, $pad, $pad, $diameter, $diameter)
        $g.DrawEllipse($penBorder, $pad, $pad, $diameter, $diameter)

        # Foreground Symbol: Bright Solid White Lightning Bolt (#FFFFFF) for maximum contrast
        $brushBolt = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
        $pts = @(
            (New-Object System.Drawing.PointF (9.2 * $scale), (2.8 * $scale)),
            (New-Object System.Drawing.PointF (4.8 * $scale), (8.6 * $scale)),
            (New-Object System.Drawing.PointF (8.2 * $scale), (8.6 * $scale)),
            (New-Object System.Drawing.PointF (7.0 * $scale), (13.4 * $scale)),
            (New-Object System.Drawing.PointF (11.8 * $scale), (7.4 * $scale)),
            (New-Object System.Drawing.PointF (8.6 * $scale), (7.4 * $scale))
        )
        $g.FillPolygon($brushBolt, $pts)
        $g.Dispose()

        # Build raw standard DIB bytes
        $ms = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter $ms

        $maskRowSize = [Math]::Ceiling($size / 32.0) * 4
        $maskSize = $maskRowSize * $size
        $imageSize = ($size * $size * 4) + $maskSize

        # BITMAPINFOHEADER (40 bytes)
        $bw.Write([UInt32]40)          # biSize
        $bw.Write([Int32]$size)         # biWidth
        $bw.Write([Int32]($size * 2))   # biHeight (XOR + AND)
        $bw.Write([UInt16]1)           # biPlanes
        $bw.Write([UInt16]32)          # biBitCount
        $bw.Write([UInt32]0)           # biCompression (BI_RGB)
        $bw.Write([UInt32]$imageSize)   # biSizeImage
        $bw.Write([Int32]0)            # biXPelsPerMeter
        $bw.Write([Int32]0)            # biYPelsPerMeter
        $bw.Write([UInt32]0)           # biClrUsed
        $bw.Write([UInt32]0)           # biClrImportant

        # 32-bit BGRA pixels bottom-to-top
        for ($y = $size - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $size; $x++) {
                $pixel = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$pixel.B)
                $bw.Write([byte]$pixel.G)
                $bw.Write([byte]$pixel.R)
                $bw.Write([byte]$pixel.A)
            }
        }

        # 1-bit AND mask bottom-to-top
        $maskBytes = New-Object byte[] $maskSize
        $bw.Write($maskBytes, 0, $maskBytes.Length)

        $bw.Flush()
        $bmp.Dispose()

        $dibBuffers += $ms
    }

    # Write multi-image ICO binary format
    $fs = New-Object System.IO.FileStream $icoPath, ([System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter $fs

    $bw.Write([UInt16]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)

    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $dibBytes = $dibBuffers[$i].ToArray()

        $bw.Write([byte]$s)
        $bw.Write([byte]$s)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([UInt16]1)
        $bw.Write([UInt16]32)
        $bw.Write([UInt32]$dibBytes.Length)
        $bw.Write([UInt32]$offset)

        $offset += $dibBytes.Length
    }

    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dibBytes = $dibBuffers[$i].ToArray()
        $bw.Write($dibBytes, 0, $dibBytes.Length)
        $dibBuffers[$i].Dispose()
    }

    $bw.Flush()
    $bw.Close()
    $fs.Close()

    Write-Host "High-Visibility DIB Icon created at $icoPath"
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = Get-Location }
$targetIco = Join-Path $scriptDir "app.ico"
Create-HighVisibilityIcon $targetIco
