$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$framework = (Get-ChildItem 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\PresentationFramework' -Recurse -Filter PresentationFramework.dll | Select-Object -First 1).FullName
$core = (Get-ChildItem 'C:\Windows\Microsoft.NET\assembly\GAC_32\PresentationCore' -Recurse -Filter PresentationCore.dll | Select-Object -First 1).FullName
$windowsBase = (Get-ChildItem 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\WindowsBase' -Recurse -Filter WindowsBase.dll | Select-Object -First 1).FullName
$xaml = (Get-ChildItem 'C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml' -Recurse -Filter System.Xaml.dll | Select-Object -First 1).FullName
& $compiler /nologo /target:winexe /platform:anycpu "/out:$root\CoopLauncher.exe" "/win32icon:$root\CoopLauncher.ico" "/reference:$framework" "/reference:$core" "/reference:$windowsBase" "/reference:$xaml" /reference:System.Core.dll /reference:System.Web.Extensions.dll /reference:System.Xml.dll "/resource:$root\CoopLauncher.xaml,CoopLauncher.xaml" "/resource:$root\BattleBlock-cover.jpg,BattleBlock-cover.jpg" "/resource:$root\HollowKnight-header.jpg,HollowKnight-header.jpg" "$root\CoopLauncherCore.cs" "$root\CoopLauncherModern.cs" "$root\CoopUpdates.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output 'CoopLauncher.exe compilado.'

