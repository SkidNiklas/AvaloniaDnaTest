Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force src/AvaloniaDnaTest.AddIn/Icons | Out-Null
foreach ($state in @('Ready','Loading','Open')) {
    $bitmap = [System.Drawing.Bitmap]::new(32,32)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = 'AntiAlias'
    $color = switch ($state) { Ready { '#2277CC' } Loading { '#E88A18' } Open { '#229955' } }
    $pen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml($color),3)
    if ($state -eq 'Loading') {
        $graphics.DrawArc($pen,5,5,22,22,30,285)
        $graphics.DrawLine($pen,25,5,26,12)
        $graphics.DrawLine($pen,26,12,19,11)
    } else {
        $graphics.DrawRectangle($pen,4,6,24,20)
        $graphics.DrawLine($pen,4,12,28,12)
    }
    $bitmap.Save((Join-Path (Get-Location) "src/AvaloniaDnaTest.AddIn/Icons/$state.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $pen.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}
