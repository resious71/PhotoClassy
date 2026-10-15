<#
  PhotoClassy 실제 화면 캡처 스크립트 (Windows PowerShell 5.1)
  1) 샘플 사진/동영상 생성  2) 앱 실행  3) UI 자동화로 분류 시작  4) 각 화면 캡처
  사용: powershell -File Capture-Screenshots.ps1 -AppDir <PhotoClassy.exe 폴더> -OutDir <PNG 저장 폴더>
#>
param(
    [Parameter(Mandatory = $true)][string]$AppDir,
    [Parameter(Mandatory = $true)][string]$OutDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, System.Windows.Forms, UIAutomationClient, UIAutomationTypes, Microsoft.VisualBasic
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT rect, int size);
    // 그림자/투명 테두리를 뺀 실제 창 영역
    public static RECT FrameBounds(IntPtr hWnd) {
        RECT r; DwmGetWindowAttribute(hWnd, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out r, Marshal.SizeOf(typeof(RECT)));
        return r;
    }
}
"@

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Get-ChildItem $OutDir -File | Remove-Item -Force

try { Set-DisplayResolution -Width 1920 -Height 1080 -Force -ErrorAction Stop; Start-Sleep -Seconds 2 } catch { Write-Host "Set-DisplayResolution unavailable: $_" }
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host "Screen: $($screen.Width)x$($screen.Height)"

# ---------------------------------------------------------------- 샘플 데이터
$demo = 'C:\PhotoClassyDemo'
if (Test-Path $demo) { Remove-Item $demo -Recurse -Force }
$camera = Join-Path $demo 'Camera'
$phone = Join-Path $demo 'Phone'
$videos = Join-Path $demo 'Videos'
$target = Join-Path $demo 'Sorted'
foreach ($d in @($camera, $phone, $videos, $target, (Join-Path $camera '2025_Jeju'), (Join-Path $phone 'Screenshots'))) {
    New-Item -ItemType Directory -Force -Path $d | Out-Null
}

$rand = New-Object System.Random 42
$palette = @(
    @([System.Drawing.Color]::FromArgb(79, 141, 247), [System.Drawing.Color]::FromArgb(36, 89, 201)),
    @([System.Drawing.Color]::FromArgb(255, 183, 77), [System.Drawing.Color]::FromArgb(239, 108, 0)),
    @([System.Drawing.Color]::FromArgb(102, 187, 106), [System.Drawing.Color]::FromArgb(46, 125, 50)),
    @([System.Drawing.Color]::FromArgb(240, 98, 146), [System.Drawing.Color]::FromArgb(173, 20, 87))
)

function New-Photo([string]$Path, [datetime]$Date, [bool]$WithExif, [System.Drawing.Imaging.ImageFormat]$Format) {
    $bmp = New-Object System.Drawing.Bitmap 1600, 1200
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $colors = $palette[$rand.Next($palette.Count)]
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle 0, 0, 1600, 1200), $colors[0], $colors[1], 45)
    $g.FillRectangle($brush, 0, 0, 1600, 1200)
    for ($i = 0; $i -lt 40; $i++) {
        $c = [System.Drawing.Color]::FromArgb(60, $rand.Next(256), $rand.Next(256), $rand.Next(256))
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $c), $rand.Next(1600), $rand.Next(1200), $rand.Next(50, 400), $rand.Next(50, 400))
    }
    $font = New-Object System.Drawing.Font 'Segoe UI', 64
    $g.DrawString($Date.ToString('yyyy-MM-dd'), $font, [System.Drawing.Brushes]::White, 60, 1040)
    $g.Dispose()

    if ($WithExif) {
        $item = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject([System.Drawing.Imaging.PropertyItem])
        $bytes = [System.Text.Encoding]::ASCII.GetBytes($Date.ToString('yyyy:MM:dd HH:mm:ss') + [char]0)
        $item.Id = 0x9003; $item.Type = 2; $item.Len = $bytes.Length; $item.Value = $bytes
        $bmp.SetPropertyItem($item)
    }
    $bmp.Save($Path, $Format)
    $bmp.Dispose()
    # 수정일은 일부러 다르게 (EXIF 날짜가 우선함을 보여주기 위해)
    [System.IO.File]::SetLastWriteTime($Path, (Get-Date))
}

$start = Get-Date '2024-03-01'
$photos = @()
for ($i = 1; $i -le 36; $i++) {
    $date = $start.AddDays($rand.Next(0, 900)).AddHours($rand.Next(8, 20))
    $folder = if ($i % 4 -eq 0) { Join-Path $camera '2025_Jeju' } else { $camera }
    $path = Join-Path $folder ('IMG_{0:0000}.jpg' -f (1000 + $i))
    New-Photo $path $date $true ([System.Drawing.Imaging.ImageFormat]::Jpeg)
    $photos += $path
}
# 스크린샷 (EXIF 없음 → 수정일로 분류)
for ($i = 1; $i -le 12; $i++) {
    $date = $start.AddDays($rand.Next(0, 900))
    $path = Join-Path (Join-Path $phone 'Screenshots') ('Screenshot_{0:yyyyMMdd}_{1:00}.png' -f $date, $i)
    New-Photo $path $date $false ([System.Drawing.Imaging.ImageFormat]::Png)
    [System.IO.File]::SetLastWriteTime($path, $date)
}
# 메신저로 받은 같은 사진 (중복)
for ($i = 0; $i -lt 6; $i++) {
    $copy = Join-Path $phone ('KakaoTalk_Photo_{0:00}.jpg' -f $i)
    Copy-Item $photos[$i * 5] $copy
}
# 같은 파일명, 다른 내용 (이름 변경 _1)
New-Photo (Join-Path $phone 'IMG_1001.jpg') (Get-Date '2024-03-15') $false ([System.Drawing.Imaging.ImageFormat]::Jpeg)
[System.IO.File]::SetLastWriteTime((Join-Path $phone 'IMG_1001.jpg'), (Get-Date '2024-03-15'))
# 동영상 (진행 화면을 캡처할 수 있도록 파일 수를 넉넉히. 크기는 모두 달라 해시 계산은 생략됨)
for ($i = 1; $i -le 300; $i++) {
    $buffer = New-Object byte[] (2MB + $i * 13)
    $rand.NextBytes($buffer)
    $path = Join-Path $videos ('VID_{0:0000}.mp4' -f $i)
    [System.IO.File]::WriteAllBytes($path, $buffer)
    [System.IO.File]::SetLastWriteTime($path, $start.AddDays($rand.Next(0, 900)))
}
# 휴대폰 백업 (작은 파일 다수)
$backup = Join-Path $phone 'Backup'
New-Item -ItemType Directory -Force -Path $backup | Out-Null
for ($i = 1; $i -le 6000; $i++) {
    $buffer = New-Object byte[] (20000 + $i)
    $rand.NextBytes($buffer)
    $path = Join-Path $backup ('IMG_{0:00000}.heic' -f $i)
    [System.IO.File]::WriteAllBytes($path, $buffer)
    [System.IO.File]::SetLastWriteTime($path, $start.AddDays($rand.Next(0, 900)))
}
# 숨김/시스템 파일 (항상 제외)
$thumbs = Join-Path $camera 'Thumbs.db'
Set-Content -Path $thumbs -Value 'thumbs'
(Get-Item $thumbs -Force).Attributes = 'Hidden, System'

# ---------------------------------------------------------------- 설정
$config = @{
    sourceFolders        = @($camera, $phone, $videos)
    targetFolder         = $target
    classificationFormat = 'YYYY/MM'
    fileOperation        = 'Copy'
    includingExtensions  = @()
    BarringExtensions    = @('.txt')
} | ConvertTo-Json
[System.IO.File]::WriteAllText((Join-Path $AppDir 'config.json'), $config, (New-Object System.Text.UTF8Encoding $false))

# ---------------------------------------------------------------- UI 자동화 도우미
$root = [System.Windows.Automation.AutomationElement]::RootElement
$scope = [System.Windows.Automation.TreeScope]

function Find-Window([string]$AutomationId, [int]$TimeoutSeconds = 60) {
    $cond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::AutomationIdProperty), $AutomationId
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $w = Find-WindowNow $cond
        if ($w) { return $w }
        Start-Sleep -Milliseconds 200
    }
    Save-Screen 'error-screen'
    throw "Window '$AutomationId' not found"
}

# WPF 의 소유된 대화상자는 UI 자동화 트리에서 소유자 창의 자식으로 나타난다.
function Find-WindowNow($cond) {
    $w = $root.FindFirst($scope::Children, $cond)
    if ($w) { return $w }
    $pidCond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::ProcessIdProperty), $process.Id
    foreach ($top in $root.FindAll($scope::Children, $pidCond)) {
        $w = $top.FindFirst($scope::Children, $cond)
        if ($w) { return $w }
    }
    return $null
}

function Save-Screen([string]$Name) {
    $bmp = New-Object System.Drawing.Bitmap $screen.Width, $screen.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save((Join-Path $OutDir "$Name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Invoke-Button($Window, [string]$AutomationId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::AutomationIdProperty), $AutomationId
    $button = $Window.FindFirst($scope::Descendants, $cond)
    if (-not $button) { throw "Button '$AutomationId' not found" }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Save-Window($Window, [string]$Name, [int]$DelayMs = 700) {
    $hwnd = [IntPtr]$Window.Current.NativeWindowHandle
    [Win32]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds $DelayMs
    $r = [Win32]::FrameBounds($hwnd)
    $bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $g.Dispose()
    $file = Join-Path $OutDir "$Name.png"
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Saved $file ($($r.Right - $r.Left)x$($r.Bottom - $r.Top))"
}

# ---------------------------------------------------------------- 캡처
$process = Start-Process -FilePath (Join-Path $AppDir 'PhotoClassy.exe') -WorkingDirectory $AppDir -PassThru
try {
    $main = Find-Window 'MainWindow'
    $hwnd = [IntPtr]$main.Current.NativeWindowHandle
    $height = [Math]::Min(860, $screen.Height - 40)
    [Win32]::MoveWindow($hwnd, 20, 10, 1000, $height, $true) | Out-Null
    Start-Sleep -Seconds 2
    Save-Window $main 'main'

    Invoke-Button $main 'StartButton'
    $dupCond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::AutomationIdProperty), 'DuplicateConfirmWindow'
    for ($i = 1; $i -le 40; $i++) {
        Start-Sleep -Milliseconds 300
        if (Find-WindowNow $dupCond) { break }
        Save-Window $main ('progress-{0:00}' -f $i) 50
    }

    $dup = Find-Window 'DuplicateConfirmWindow' 300
    Save-Window $dup 'duplicates'
    Invoke-Button $dup 'KeepButton'

    # 진행 화면은 후반부(이동/복사 단계) 하나만 남긴다
    $shots = @(Get-ChildItem $OutDir -Filter 'progress-*.png' | Sort-Object Name)
    if ($shots.Count -gt 0) {
        Copy-Item $shots[[int][Math]::Floor($shots.Count * 0.6)].FullName (Join-Path $OutDir 'progress.png')
        $shots | Remove-Item -Force
    }

    $summary = Find-Window 'SummaryWindow' 60
    Save-Window $summary 'summary'
    Invoke-Button $summary 'OkButton'

    Start-Sleep -Seconds 2
    Save-Window $main 'completed'

    # 결과 폴더 구조
    Get-ChildItem $target -Directory -Recurse | ForEach-Object { $_.FullName.Substring($target.Length) } | Sort-Object |
        Set-Content -Path (Join-Path $OutDir 'tree.txt') -Encoding UTF8
}
finally {
    if (-not $process.HasExited) { $process.Kill() }
}
