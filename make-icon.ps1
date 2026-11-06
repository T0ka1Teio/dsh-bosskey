Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(1, [int]($s * 0.04))
    $r = [Math]::Max(2, [int]($s * 0.22))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $s - 2 * $pad
    $path.AddArc($pad, $pad, $r, $r, 180, 90)
    $path.AddArc($pad + $w - $r, $pad, $r, $r, 270, 90)
    $path.AddArc($pad + $w - $r, $pad + $w - $r, $r, $r, 0, 90)
    $path.AddArc($pad, $pad + $w - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point 0, 0),
        (New-Object System.Drawing.Point $s, $s),
        [System.Drawing.Color]::FromArgb(255, 42, 108, 232),
        [System.Drawing.Color]::FromArgb(255, 18, 58, 150))
    $g.FillPath($brush, $path)

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([Math]::Max(1.4, $s * 0.075))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $cx = $s * 0.5; $cy = $s * 0.47; $rad = $s * 0.235
    $g.DrawEllipse($pen, ($cx - $rad), ($cy - $rad), (2 * $rad), (2 * $rad))
    $k = $s * 0.175
    $g.DrawLine($pen, ($cx - $k), ($cy + $k), ($cx + $k), ($cy - $k))

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose(); $g.Dispose(); $bmp.Dispose(); $path.Dispose(); $brush.Dispose(); $pen.Dispose()
    return , $bytes
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = @()
foreach ($s in $sizes) { $images += , (New-IconPng $s) }

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $data = $images[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim); $bw.Write([Byte]$dim)
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($d in $images) { $bw.Write($d) }
$bw.Flush()
[System.IO.File]::WriteAllBytes('F:\AIproject\temp\dsh-bosskey\icon.ico', $ms.ToArray())
$bw.Dispose(); $ms.Dispose()
"icon.ico written: " + (Get-Item 'F:\AIproject\temp\dsh-bosskey\icon.ico').Length + " bytes"
