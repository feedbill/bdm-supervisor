param([string]$Tabs = "all", [int]$Wait = 30, [string]$Lang = "")
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class M { [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint x,uint y,uint d,UIntPtr e);
 public static void Click(int x,int y){ SetCursorPos(x,y); mouse_event(2,0,0,0,UIntPtr.Zero); mouse_event(4,0,0,0,UIntPtr.Zero);} }
"@
$out = "C:\feedbill\shots"; New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -Filter *.png | Remove-Item -Force
"start $(Get-Date)" | Set-Content "$out\log.txt"
[System.Windows.Forms.SendKeys]::SendWait("{ESC}")
Get-Process BdmSupervisor -EA SilentlyContinue | Stop-Process -Force
Start-Sleep 2
Start-Process "C:\feedbill\BdmSupervisor\publish\BdmSupervisor.exe"
Start-Sleep $Wait
function Shot($n) { $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height; $g = [Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($b.Location, [Drawing.Point]::Empty, $b.Size); $bmp.Save("$out\$n.png"); $g.Dispose(); $bmp.Dispose() }
$root = [Windows.Automation.AutomationElement]::RootElement
$cond = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::Window)
$win = $root.FindAll('Children', $cond) | Where-Object { $_.Current.Name -like '*Supervisor*' } | Select-Object -First 1
if (-not $win) { "no window" | Add-Content "$out\log.txt"; Shot "nowin"; exit }
$tc = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::ControlTypeProperty, [Windows.Automation.ControlType]::TabItem)
$items = $win.FindAll('Descendants', $tc)
$i = 0
foreach ($t in $items) {
  $n = $t.Current.Name; $i++
  "tab $i $n" | Add-Content "$out\log.txt"
  if ($Tabs -ne "all" -and ($Tabs.Split(',') -notcontains "$i")) { continue }
  $r = $t.Current.BoundingRectangle
  [M]::Click([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2))
  Start-Sleep 12
  Shot ("{0:D2}" -f $i)
}
"end $(Get-Date)" | Add-Content "$out\log.txt"
