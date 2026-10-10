Add-Type -AssemblyName System.Drawing
$frames = @()
foreach ($size in @(16,32,48,64,128,256)) {
 $bmp = New-Object System.Drawing.Bitmap($size,$size)
 $g = [System.Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.Clear([System.Drawing.Color]::Transparent)
 $g.ScaleTransform(($size / 64.0),($size / 64.0))
 $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(168,217,188))
 $ink = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(29,49,38))
 $paper = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(247,249,237))
 $g.FillRectangle($bg,8,2,48,60); $g.FillRectangle($bg,2,8,60,48)
 foreach ($xy in @(@(2,2),@(50,2),@(2,50),@(50,50))) { $g.FillEllipse($bg,$xy[0],$xy[1],12,12) }
 $g.FillRectangle($ink,13,16,38,35)
 $g.FillRectangle($paper,17,20,13,25); $g.FillRectangle($paper,34,20,13,25)
 $g.FillRectangle($ink,31,18,2,34)
 $g.FillRectangle($ink,39,18,4,15)
 $stream = New-Object System.IO.MemoryStream
 $bmp.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
 $frames += ,@($size,$stream.ToArray())
 $stream.Dispose(); $g.Dispose(); $bmp.Dispose(); $bg.Dispose(); $ink.Dispose(); $paper.Dispose()
}
$output = Join-Path $PSScriptRoot '..\app.ico'
$file = [System.IO.File]::Create($output)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
 $dim = if ($frame[0] -eq 256) { 0 } else { $frame[0] }
 $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
 $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame[1].Length); $writer.Write([uint32]$offset)
 $offset += $frame[1].Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame[1]) }
$writer.Dispose(); $file.Dispose()
