$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$culture = [Globalization.CultureInfo]::InvariantCulture
$typeface = [Windows.Media.Typeface]::new([Windows.Media.FontFamily]::new('Segoe UI'), [Windows.FontStyles]::Normal, [Windows.FontWeights]::Bold, [Windows.FontStretches]::Normal)
$text = [Windows.Media.FormattedText]::new('TK', $culture, [Windows.FlowDirection]::LeftToRight, $typeface, 256, [Windows.Media.Brushes]::White)
$letters = $text.BuildGeometry([Windows.Point]::new(0, 0))
$bounds = $letters.Bounds
$scale = [Math]::Min(360 / $bounds.Width, 218 / $bounds.Height)
$x = (512 - $bounds.Width * $scale) / 2 - $bounds.X * $scale
$y = 136 - $bounds.Y * $scale
$transform = [Windows.Media.MatrixTransform]::new([Windows.Media.Matrix]::new($scale,0,0,$scale,$x,$y))
$path = $letters.GetOutlinedPathGeometry().ToString($culture) -replace '^F[01]\s*', ''
$matrix = @($scale,0,0,$scale,$x,$y) | ForEach-Object { ([double]$_).ToString('0.########', $culture) }
$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" role="img" aria-labelledby="title">
  <title id="title">Thong Khong Toolbox</title>
  <rect x="8" y="8" width="496" height="496" rx="96" fill="#17263E"/>
  <path fill="#FFFFFF" transform="matrix($($matrix -join ' '))" d="$path"/>
  <g fill="#397CE5">
    <rect x="400" y="377" width="34" height="34" rx="7"/>
    <rect x="359" y="418" width="34" height="34" rx="7"/>
    <rect x="400" y="418" width="34" height="34" rx="7"/>
  </g>
</svg>
"@
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'toolbox-logo.svg'), $svg)
function Render-Logo([int]$Size) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    try {
        $context.PushTransform([Windows.Media.ScaleTransform]::new($Size / 512.0, $Size / 512.0))
        $navy = [Windows.Media.BrushConverter]::new().ConvertFromString('#17263E')
        $blue = [Windows.Media.BrushConverter]::new().ConvertFromString('#397CE5')
        $context.DrawRoundedRectangle($navy, $null, [Windows.Rect]::new(8,8,496,496),96,96)
        $context.PushTransform($transform)
        $context.DrawGeometry([Windows.Media.Brushes]::White,$null,$letters)
        $context.Pop()
        foreach ($position in @(@(400,377),@(359,418),@(400,418))) {
            $context.DrawRoundedRectangle($blue,$null,[Windows.Rect]::new($position[0],$position[1],34,34),7,7)
        }
        $context.Pop()
    } finally { $context.Close() }
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($Size,$Size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    try { $encoder.Save($stream); return ,$stream.ToArray() } finally { $stream.Dispose() }
}
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'toolbox-logo.png'), (Render-Logo 1024))
$sizes = @(16,24,32,48,64,128,256)
$frames = @($sizes | ForEach-Object { ,(Render-Logo $_) })
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $PSScriptRoot 'toolbox.ico')))
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
