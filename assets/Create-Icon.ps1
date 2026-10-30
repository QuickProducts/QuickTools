# Package the approved transparent PNG into a multi-resolution Windows icon.
Add-Type -AssemblyName System.Drawing
$sizes=@(16,24,32,48,64,128,256)
$images=[System.Collections.Generic.List[byte[]]]::new()
$source=[System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'quicktools-logo-glass.png'))
try {
 foreach($size in $sizes) {
  $bitmap=[System.Drawing.Bitmap]::new($size,$size)
  $g=[System.Drawing.Graphics]::FromImage($bitmap)
  try {
   $g.Clear([System.Drawing.Color]::Transparent)
   $g.CompositingQuality=[System.Drawing.Drawing2D.CompositingQuality]::HighQuality
   $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
   $g.DrawImage($source,[System.Drawing.Rectangle]::new(0,0,$size,$size))
   $stream=[System.IO.MemoryStream]::new()
   try { $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png); $images.Add($stream.ToArray()) } finally { $stream.Dispose() }
  } finally { $g.Dispose(); $bitmap.Dispose() }
 }
} finally { $source.Dispose() }
$output=[System.IO.BinaryWriter]::new([System.IO.File]::Create((Join-Path $PSScriptRoot 'QuickTools.ico')))
try {
 $output.Write([uint16]0); $output.Write([uint16]1); $output.Write([uint16]$sizes.Count)
 $offset=6+16*$sizes.Count
 for($i=0;$i -lt $sizes.Count;$i++) {
  $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
  $output.Write([byte]$dimension); $output.Write([byte]$dimension); $output.Write([byte]0); $output.Write([byte]0)
  $output.Write([uint16]1); $output.Write([uint16]32); $output.Write([uint32]$images[$i].Length); $output.Write([uint32]$offset)
  $offset+=$images[$i].Length
 }
 foreach($bytes in $images) { $output.Write($bytes) }
} finally { $output.Dispose() }
