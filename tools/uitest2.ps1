$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class M { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e);
 public static void Click(int x,int y){ SetCursorPos(x,y); mouse_event(2,0,0,0,UIntPtr.Zero); mouse_event(4,0,0,0,UIntPtr.Zero);} }
"@
$out = "C:\fbtmp\bdmsup\shots"; New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -Filter *.png | Remove-Item -Force
"start $(Get-Date)" | Set-Content "$out\log.txt"
function Log($t) { "$(Get-Date -f HH:mm:ss) $t" | Add-Content "$out\log.txt" }
function Shot($n) { $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height; $g = [Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($b.Location, [Drawing.Point]::Empty, $b.Size); $bmp.Save("$out\$n.png"); $g.Dispose(); $bmp.Dispose(); Log "shot $n" }
Get-Process BdmSupervisor -EA SilentlyContinue | Stop-Process -Force
Start-Sleep 2
Start-Process "C:\fbtmp\bdmsup\BdmSupervisor.exe"
Start-Sleep 25
$root = [Windows.Automation.AutomationElement]::RootElement
$wc = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Window)
$win = $root.FindAll('Children', $wc) | ? { $_.Current.Name -like '*Supervisor*' } | Select -First 1
if (-not $win) { Log "no window"; Shot nowin; exit }
function ClickEl($type, $pattern) {
  $c = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
  $e = $win.FindAll('Descendants', $c) | ? { $_.Current.Name -like $pattern } | Select -First 1
  if (-not $e) { Log "no encontrado $pattern"; return }
  $r = $e.Current.BoundingRectangle; [M]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2)); Log "click $pattern"
}
$T = [Windows.Automation.ControlType]::TabItem; $B = [Windows.Automation.ControlType]::Button
ClickEl $T 'Impresora*'; Start-Sleep 8; Shot p1
ClickEl $B 'IMPRIMIR*'; Start-Sleep 15; Shot p2
ClickEl $T 'Teclado*'; Start-Sleep 8; Shot k1
ClickEl $T 'Sensores*'; Start-Sleep 8; Shot s1
ClickEl $B 'PROBAR INDICADORES*'; Start-Sleep 3; Shot s2
Start-Sleep 10; Shot s3
Get-Process BdmSupervisor -EA SilentlyContinue | Stop-Process -Force
Log "end"
