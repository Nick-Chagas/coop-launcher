param(
    [ValidateSet('original', 'family')]
    [string]$Mode,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$game = $PSScriptRoot
$managed = Join-Path $game 'hollow_knight_Data\Managed'
$profiles = Join-Path $game 'CoopProfiles'
$modDirectory = Join-Path $managed 'Mods\HollowKnightLocal8'
$activeMod = Join-Path $modDirectory 'HollowKnightLocal8.dll'
$exe = Join-Path $game 'hollow_knight.exe'

foreach ($required in @($exe)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Arquivo necessario nao encontrado: $required"
    }
}

if (-not $Mode) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [System.Windows.Forms.Application]::EnableVisualStyles()
    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'Hollow Knight - Escolher modo'
    $form.Size = New-Object System.Drawing.Size(500, 230)
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.BackColor = [System.Drawing.Color]::FromArgb(21, 28, 39)
    $form.ForeColor = [System.Drawing.Color]::White

    $title = New-Object System.Windows.Forms.Label
    $title.Text = 'Como vamos jogar?'
    $title.Font = New-Object System.Drawing.Font('Segoe UI', 16, [System.Drawing.FontStyle]::Bold)
    $title.AutoSize = $true
    $title.Location = New-Object System.Drawing.Point(22, 20)
    $form.Controls.Add($title)

    $options = @(
        @{ Key = 'original'; Label = 'Jogar sem mod'; Help = 'Hollow Knight original.' },
        @{ Key = 'family'; Label = 'Jogar com Family Coop'; Help = 'Coop local com ate quatro jogadores.' }
    )
    $script:SelectedMode = $null
    for ($i = 0; $i -lt $options.Count; $i++) {
        $option = $options[$i]
        $button = New-Object System.Windows.Forms.Button
        $button.Text = $option.Label
        $button.Tag = $option.Key
        $button.Location = New-Object System.Drawing.Point(22, (66 + $i * 61))
        $button.Size = New-Object System.Drawing.Size(450, 32)
        $button.BackColor = [System.Drawing.Color]::FromArgb(50, 70, 96)
        $button.ForeColor = [System.Drawing.Color]::White
        $button.FlatStyle = 'Flat'
        $button.UseVisualStyleBackColor = $false
        $button.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(104, 142, 171)
        $button.FlatAppearance.MouseOverBackColor = [System.Drawing.Color]::FromArgb(65, 95, 125)
        $button.Add_Click({ $script:SelectedMode = [string]$this.Tag; $form.Close() })
        $form.Controls.Add($button)
        $help = New-Object System.Windows.Forms.Label
        $help.Text = $option.Help
        $help.ForeColor = [System.Drawing.Color]::FromArgb(210, 223, 235)
        $help.AutoSize = $true
        $help.Location = New-Object System.Drawing.Point(27, (99 + $i * 61))
        $form.Controls.Add($help)
    }
    [void]$form.ShowDialog()
    if (-not $script:SelectedMode) { return }
    $Mode = $script:SelectedMode
}

if (Get-Process -Name 'hollow_knight' -ErrorAction SilentlyContinue) {
    throw 'Feche o Hollow Knight antes de trocar de modo.'
}

$resolvedGame = (Resolve-Path -LiteralPath $game).Path.TrimEnd('\')
$resolvedManaged = (Resolve-Path -LiteralPath $managed).Path.TrimEnd('\')
if (-not $resolvedManaged.StartsWith($resolvedGame + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'A pasta Managed nao pertence ao jogo selecionado.'
}

if ($Mode -eq 'original') {
    foreach ($name in @('Assembly-CSharp.dll', 'TeamCherry.Localization.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $profiles "original\$name") -PathType Leaf)) {
            throw "Perfil original nao encontrado: $name"
        }
    }
    foreach ($name in @('Assembly-CSharp.dll', 'TeamCherry.Localization.dll')) {
        Copy-Item -LiteralPath (Join-Path $profiles "original\$name") -Destination (Join-Path $managed $name) -Force
    }
    foreach ($apiFile in (Get-ChildItem -LiteralPath (Join-Path $profiles 'api') -File -Filter '*.dll')) {
        if ($apiFile.Name -in @('Assembly-CSharp.dll', 'TeamCherry.Localization.dll')) { continue }
        $target = Join-Path $managed $apiFile.Name
        if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
    }
    if (Test-Path -LiteralPath $activeMod -PathType Leaf) { Remove-Item -LiteralPath $activeMod -Force }
}
else {
    if (-not (Test-Path -LiteralPath (Join-Path $profiles 'family\HollowKnightLocal8.dll') -PathType Leaf)) {
        throw 'Family Coop nao instalado.'
    }
    foreach ($apiFile in (Get-ChildItem -LiteralPath (Join-Path $profiles 'api') -File -Filter '*.dll')) {
        Copy-Item -LiteralPath $apiFile.FullName -Destination (Join-Path $managed $apiFile.Name) -Force
    }
    New-Item -ItemType Directory -Force -Path $modDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $profiles "$Mode\HollowKnightLocal8.dll") -Destination $activeMod -Force
}

Write-Output "Modo instalado: $Mode"
if (-not $NoLaunch) { Start-Process -FilePath $exe -WorkingDirectory $game }
