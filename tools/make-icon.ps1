param(
    [Parameter(Mandatory = $true)]
    [string]$Out
)

Add-Type -AssemblyName System.Drawing

function New-RoundedPath {
    param([int]$X, [int]$Y, [int]$W, [int]$H, [int]$R)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $R * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$bgPath = New-RoundedPath -X 8 -Y 8 -W 240 -H 240 -R 54
$bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 28, 31, 38))
$g.FillPath($bgBrush, $bgPath)

$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 79, 140, 255), 10)
$g.DrawPath($borderPen, $bgPath)

$framePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 12)
$framePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$framePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
# Camera / capture frame with four corner brackets.
$g.DrawLine($framePen, 64, 84, 104, 84)
$g.DrawLine($framePen, 64, 84, 64, 124)
$g.DrawLine($framePen, 192, 84, 152, 84)
$g.DrawLine($framePen, 192, 84, 192, 124)
$g.DrawLine($framePen, 64, 172, 104, 172)
$g.DrawLine($framePen, 64, 172, 64, 132)
$g.DrawLine($framePen, 192, 172, 152, 172)
$g.DrawLine($framePen, 192, 172, 192, 132)

$dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 79, 140, 255))
$g.FillEllipse($dotBrush, 114, 114, 28, 28)

$g.Dispose()

$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$stream = [System.IO.File]::Create($Out)
try {
    $icon.Save($stream)
}
finally {
    $stream.Close()
    $icon.Dispose()
    $bmp.Dispose()
    $bgPath.Dispose()
    $bgBrush.Dispose()
    $borderPen.Dispose()
    $framePen.Dispose()
    $dotBrush.Dispose()
}

Write-Host ("icon written: " + $Out)
