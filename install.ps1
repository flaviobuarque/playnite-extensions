#Requires -Version 5.1
<#
.SYNOPSIS
    Instalador Universal com Interface Gráfica (WPF Wizard) - Playnite Extensions & Themes.
.DESCRIPTION
    Assistente guiado em Dark Mode, orientado a dados (catalog.json) e com suporte a instalação via Web:
    irm https://raw.githubusercontent.com/flaviobuarque/playnite-extensions/main/install.ps1 | iex
#>

param(
    [Parameter(Position=0)]
    [string]$Target = "",

    [string]$PlaynitePath = "",
    [switch]$Silent,
    [switch]$ForceClose,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

$RepoOwner = "flaviobuarque"
$RepoName = "playnite-extensions"
$RawBaseUrl = "https://raw.githubusercontent.com/$RepoOwner/$RepoName/main"
$LatestReleaseApi = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"

#Fallback embutido caso esteja offline antes da consulta ao catalog.json
$FallbackCatalogJson = @'
[
  {
    "id": "ExternalGameTracker",
    "name": "External Game Tracker",
    "type": "EXTENSÃO",
    "version": "v1.0.0",
    "shortDescription": "Rastreamento automático de tempo e status de jogos abertos fora do Playnite.",
    "accentColor": "#10B981",
    "iconBg": "#112E28",
    "badgeBg": "#1D2B44",
    "badgeFg": "#60A5FA",
    "iconPath": "M12,20A7,7 0 0,1 5,13A7,7 0 0,1 12,6A7,7 0 0,1 19,13A7,7 0 0,1 12,20M19.03,7.39L20.45,5.97C20,5.46 19.55,5 19.04,4.56L17.62,6C16.07,4.74 14.12,4 12,4A9,9 0 0,0 3,13A9,9 0 0,0 12,22C17,22 21,17.97 21,13C21,10.88 20.26,8.93 19.03,7.39M11,14H13V8H11M15,1H9V3H15V1Z",
    "assetPattern": "ExternalGameTracker*.pext",
    "localThemeDir": "",
    "installSubdir": "Extensions\\ExternalGameTracker_6f982b13-054d-44a6-b51c-81b4d0eb38c9",
    "defaultSelected": true
  },
  {
    "id": "InstallationStatusUpdater",
    "name": "Installation Status Updater",
    "type": "EXTENSÃO",
    "version": "v2.54",
    "shortDescription": "Detecção rápida de jogos instalados nos discos e limpeza de pastas órfãs.",
    "accentColor": "#38BDF8",
    "iconBg": "#122B40",
    "badgeBg": "#1D2B44",
    "badgeFg": "#60A5FA",
    "iconPath": "M21,16.5C21,16.88 20.79,17.21 20.47,17.38L12.57,21.82C12.41,21.94 12.21,22 12,22C11.79,22 11.59,21.94 11.43,21.82L3.53,17.38C3.21,17.21 3,16.88 3,16.5V7.5C3,7.12 3.21,6.79 3.53,6.62L11.43,2.18C11.59,2.06 11.79,2 12,2C12.21,2 12.41,2.06 12.57,2.18L20.47,6.62C20.79,6.79 21,7.12 21,7.5V16.5M12,4.15L6.04,7.5L12,10.85L17.96,7.5L12,4.15M5,15.91L11,19.29V12.58L5,9.21V15.91M19,15.91V9.21L13,12.58V19.29L19,15.91Z",
    "assetPattern": "Installation_Status_Updater*.pext",
    "localThemeDir": "",
    "installSubdir": "Extensions\\Installation_Status_Updater",
    "defaultSelected": true
  },
  {
    "id": "HeliumTheme",
    "name": "Tema Helium Customizado",
    "type": "TEMA DESKTOP",
    "version": "v1.71-mod",
    "shortDescription": "Tema moderno e elegante com destaque visual em tempo real para jogos em execução.",
    "accentColor": "#FF5E3A",
    "iconBg": "#361E1D",
    "badgeBg": "#2E1F3D",
    "badgeFg": "#C084FC",
    "iconPath": "M17.5,12A1.5,1.5 0 0,1 16,10.5A1.5,1.5 0 0,1 17.5,9A1.5,1.5 0 0,1 19,10.5A1.5,1.5 0 0,1 17.5,12M14.5,8A1.5,1.5 0 0,1 13,6.5A1.5,1.5 0 0,1 14.5,5A1.5,1.5 0 0,1 16,6.5A1.5,1.5 0 0,1 14.5,8M9.5,8A1.5,1.5 0 0,1 8,6.5A1.5,1.5 0 0,1 9.5,5A1.5,1.5 0 0,1 11,6.5A1.5,1.5 0 0,1 9.5,8M6.5,12A1.5,1.5 0 0,1 5,10.5A1.5,1.5 0 0,1 6.5,9A1.5,1.5 0 0,1 8,10.5A1.5,1.5 0 0,1 6.5,12M12,3A9,9 0 0,0 3,12A9,9 0 0,0 12,21A1.5,1.5 0 0,0 13.5,19.5C13.5,19.11 13.35,18.76 13.11,18.5C12.88,18.23 12.73,17.88 12.73,17.5A1.5,1.5 0 0,1 14.23,16H16A5,5 0 0,0 21,11C21,6.58 16.97,3 12,3Z",
    "assetPattern": "Helium*.zip",
    "localThemeDir": "themes\\Helium",
    "installSubdir": "Themes\\Desktop\\8b15c46a-90c2-4fe5-9ebb-1ab25ba7fcb1",
    "defaultSelected": true
  }
]
'@

# --- FUNÇÕES CORE & SEGURANÇA ---
function Test-SafeGitHubUrl([string]$url) {
    if ([string]::IsNullOrWhiteSpace($url)) { return $false }
    try {
        $uri = [Uri]$url
        if ($uri.Scheme -ne "https") { return $false }
        $allowedHosts = @("github.com", "raw.githubusercontent.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com")
        if ($allowedHosts -contains $uri.Host.ToLower()) { return $true }
        return $false
    } catch {
        return $false
    }
}

function Find-DefaultPlaynitePath {
    $defaultPath = "$env:LOCALAPPDATA\Playnite"
    if (Test-Path "$defaultPath\Playnite.DesktopApp.exe") { return $defaultPath }

    $regPaths = @("HKCU:\Software\Playnite", "HKLM:\SOFTWARE\Playnite", "HKLM:\SOFTWARE\WOW6432Node\Playnite")
    foreach ($reg in $regPaths) {
        if (Test-Path $reg) {
            $val = (Get-ItemProperty -Path $reg -ErrorAction SilentlyContinue).InstallDir
            if ($val -and (Test-Path "$val\Playnite.DesktopApp.exe")) { return $val }
        }
    }
    return $defaultPath
}

function Load-CatalogItems([string]$scriptRoot) {
    $rawItems = $null
    $localCatalog = if ($scriptRoot) { Join-Path $scriptRoot "catalog.json" } else { "" }

    if ($localCatalog -and (Test-Path $localCatalog)) {
        try {
            $rawItems = Get-Content -Path $localCatalog -Raw -Encoding UTF8 | ConvertFrom-Json
        } catch {}
    }

    if (-not $rawItems) {
        try {
            $catalogUrl = "$RawBaseUrl/catalog.json"
            if (Test-SafeGitHubUrl $catalogUrl) {
                $rawItems = Invoke-RestMethod -Uri $catalogUrl -UseBasicParsing -TimeoutSec 8
            }
        } catch {}
    }

    if (-not $rawItems) {
        $rawItems = $FallbackCatalogJson | ConvertFrom-Json
    }

    $list = @()
    foreach ($r in $rawItems) {
        $list += @{
            Id               = [string]$r.id
            Name             = [string]$r.name
            Type             = [string]$r.type
            Version          = [string]$r.version
            Short            = [string]$r.shortDescription
            Accent           = [string]$r.accentColor
            IconBg           = [string]$r.iconBg
            BadgeBg          = [string]$r.badgeBg
            BadgeFg          = [string]$r.badgeFg
            IconPath         = [string]$r.iconPath
            AssetPattern     = [string]$r.assetPattern
            LocalThemeDir    = [string]$r.localThemeDir
            InstallSubdir    = [string]$r.installSubdir
            DefaultSelected  = [bool]$r.defaultSelected
            Selected         = [bool]$r.defaultSelected
            InstallStatus    = "NotInstalled"
            StatusLabel      = "NÃO INSTALADO"
            StatusBg         = "#1E293B"
            StatusFg         = "#94A3B8"
            ActionWord       = "Instalado"
        }
    }
    return $list
}

function Expand-SafeZipArchive([string]$zipPath, [string]$destinationDir) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $fullDestRoot = [System.IO.Path]::GetFullPath($destinationDir)
    if (-not $fullDestRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $fullDestRoot += [System.IO.Path]::DirectorySeparatorChar
    }
    New-Item -ItemType Directory -Path $fullDestRoot -Force | Out-Null

    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.FullName)) { continue }
            $normalizedEntry = $entry.FullName -replace '/', '\'
            $destPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($fullDestRoot, $normalizedEntry))

            # Proteção contra Path Traversal (Zip Slip)
            if (-not $destPath.StartsWith($fullDestRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Entrada insegura detectada no pacote: $($entry.FullName)"
            }

            if ($entry.FullName.EndsWith("/") -or $entry.FullName.EndsWith("\")) {
                New-Item -ItemType Directory -Path $destPath -Force | Out-Null
            } else {
                $parent = [System.IO.Path]::GetDirectoryName($destPath)
                if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destPath, $true)
            }
        }
    } finally {
        $archive.Dispose()
    }
}

$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { "" }

# --- MODO SILENCIOSO / CLI (Opcional via -Silent) ---
if ($Silent) {
    $targetDir = if ($PlaynitePath) { $PlaynitePath } else { Find-DefaultPlaynitePath }
    if (-not (Test-Path (Join-Path $targetDir "Playnite.DesktopApp.exe"))) {
        Write-Error "Playnite não localizado em: $targetDir"
        exit 1
    }

    $procs = Get-Process -Name "*Playnite*" -ErrorAction SilentlyContinue
    if ($procs) {
        if ($ForceClose) {
            $procs | Stop-Process -Force
            Start-Sleep -Seconds 1
        } else {
            Write-Error "Playnite está aberto. Use -ForceClose para encerrar automaticamente."
            exit 1
        }
    }

    $catalog = Load-CatalogItems $scriptRoot
    $targets = if ([string]::IsNullOrWhiteSpace($Target) -or $Target -eq "All") {
        $catalog
    } else {
        $ids = $Target -split ","
        $catalog | Where-Object { $ids -contains $_.Id }
    }

    if (-not $targets) {
        Write-Error "Nenhum componente correspondente ao alvo '$Target'."
        exit 1
    }

    $release = Invoke-RestMethod -Uri $LatestReleaseApi -UseBasicParsing
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("PlayniteInst_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    try {
        foreach ($item in $targets) {
            Write-Host "Instalando $($item.Name)..." -ForegroundColor Cyan
            $dest = Join-Path $targetDir $item.InstallSubdir
            $localTheme = if ($scriptRoot -and $item.LocalThemeDir) { Join-Path $scriptRoot $item.LocalThemeDir } else { "" }
            if ($localTheme -and (Test-Path $localTheme)) {
                New-Item -ItemType Directory -Path $dest -Force | Out-Null
                Copy-Item -Path "$localTheme\*" -Destination $dest -Recurse -Force
            } else {
                $asset = $release.assets | Where-Object { $_.name -like $item.AssetPattern } | Select-Object -First 1
                if (-not $asset) { throw "Asset $($item.AssetPattern) não encontrado na release $($release.tag_name)." }
                if (-not (Test-SafeGitHubUrl $asset.browser_download_url)) { throw "URL não confiável bloqueada: $($asset.browser_download_url)" }
                $dlFile = Join-Path $tempDir $asset.name
                Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $dlFile -UseBasicParsing
                Expand-SafeZipArchive $dlFile $dest
            }
            Write-Host "✅ $($item.Name) instalado!" -ForegroundColor Green
        }
        if (-not $NoLaunch) {
            Start-Process (Join-Path $targetDir "Playnite.DesktopApp.exe")
        }
    } finally {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    exit 0
}

# --- MODO GUI (WPF WIZARD) ---
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    if ($PSCommandPath) {
        Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -STA -File `"$PSCommandPath`""
        exit
    } else {
        Write-Warning "Este assistente gráfico requer PowerShell em modo STA. Execute no PowerShell padrão do Windows."
        exit 1
    }
}

[xml]$xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Playnite Extensions &amp; Themes - Instalador"
        Width="760" Height="560"
        WindowStartupLocation="CenterScreen"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ResizeMode="NoResize">

    <Window.Resources>
        <SolidColorBrush x:Key="BgDark" Color="#0F1420"/>
        <SolidColorBrush x:Key="BgHeader" Color="#151C2C"/>
        <SolidColorBrush x:Key="BgCard" Color="#182032"/>
        <SolidColorBrush x:Key="BorderSubtle" Color="#28354D"/>

        <Style x:Key="PrimaryBtn" TargetType="Button">
            <Setter Property="Background" Value="#FF5E3A"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Padding" Value="18,9"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="6" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#FF7454"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#E04B28"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="border" Property="Background" Value="#2A3449"/>
                                <Setter Property="Foreground" Value="#64748B"/>
                                <Setter Property="Cursor" Value="Arrow"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key="SuccessBtn" TargetType="Button" BasedOn="{StaticResource PrimaryBtn}">
            <Setter Property="Background" Value="#10B981"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="6" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#14CF92"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#0D9668"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="border" Property="Background" Value="#2A3449"/>
                                <Setter Property="Foreground" Value="#64748B"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key="SecondaryBtn" TargetType="Button">
            <Setter Property="Background" Value="#1E283D"/>
            <Setter Property="Foreground" Value="#E2E8F0"/>
            <Setter Property="FontWeight" Value="Medium"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Padding" Value="15,8"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border" Background="{TemplateBinding Background}" BorderBrush="#31405E" BorderThickness="1" CornerRadius="6" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#27344E"/>
                                <Setter TargetName="border" Property="BorderBrush" Value="#475C85"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="border" Property="Opacity" Value="0.45"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key="GhostBtn" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="#94A3B8"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="4" Padding="8,4">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Foreground" Value="#F1F5F9"/>
                                <Setter TargetName="border" Property="Background" Value="#1E283D"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="8"/>
            <Setter Property="MinWidth" Value="8"/>
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                        <Grid Background="Transparent">
                            <Track x:Name="PART_Track" IsDirectionReversed="True">
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType="Thumb">
                                                <Border Background="#334155" CornerRadius="4" Margin="1,0"/>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key="ModernCheckBox" TargetType="CheckBox">
            <Setter Property="Foreground" Value="#F1F5F9"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="CheckBox">
                        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                            <Border x:Name="box" Width="20" Height="20" CornerRadius="5"
                                    Background="#0F1420" BorderBrush="#475569" BorderThickness="2"
                                    VerticalAlignment="Center">
                                <Path x:Name="check" Data="M 4,10 L 8,14 L 15,5" Stroke="White" StrokeThickness="2.2"
                                      StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"
                                      Visibility="Collapsed"/>
                            </Border>
                            <ContentPresenter Margin="12,0,0,0" VerticalAlignment="Center"/>
                        </StackPanel>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="box" Property="Background" Value="#10B981"/>
                                <Setter TargetName="box" Property="BorderBrush" Value="#10B981"/>
                                <Setter TargetName="check" Property="Visibility" Value="Visible"/>
                            </Trigger>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="box" Property="BorderBrush" Value="#38BDF8"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Border Margin="12" CornerRadius="12" Background="{StaticResource BgDark}" BorderBrush="#2A3752" BorderThickness="1">
        <Border.Effect>
            <DropShadowEffect BlurRadius="20" ShadowDepth="4" Color="#000000" Opacity="0.65"/>
        </Border.Effect>

        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="48"/>
                <RowDefinition Height="52"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="66"/>
            </Grid.RowDefinitions>

            <!-- 1. BARRA DE TÍTULO -->
            <Border x:Name="TitleBar" Grid.Row="0" Background="{StaticResource BgHeader}" CornerRadius="12,12,0,0"
                    BorderBrush="#222D44" BorderThickness="0,0,0,1">
                <Grid Margin="18,0,12,0">
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <Image x:Name="HeaderLogo" Width="22" Height="22" Margin="0,0,10,0"/>
                        <TextBlock Text="Playnite Extensions &amp; Themes" FontSize="13.5" FontWeight="SemiBold" Foreground="#F8FAFC" VerticalAlignment="Center"/>
                        <Border x:Name="ReleaseTagBadge" Background="#122B40" BorderBrush="#38BDF8" BorderThickness="1" CornerRadius="4" Padding="6,2" Margin="12,0,0,0" VerticalAlignment="Center" Visibility="Collapsed">
                            <TextBlock x:Name="TxtReleaseTag" Text="" FontSize="9.5" FontWeight="Bold" Foreground="#7DD3FC"/>
                        </Border>
                    </StackPanel>

                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
                        <Button x:Name="BtnMinimize" Content="—" Width="30" Height="28" Background="Transparent" Foreground="#94A3B8" BorderThickness="0" Cursor="Hand" FontSize="12"/>
                        <Button x:Name="BtnClose" Content="✕" Width="30" Height="28" Background="Transparent" Foreground="#94A3B8" BorderThickness="0" Cursor="Hand" FontSize="13"/>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- 2. STEPPER -->
            <Border Grid.Row="1" Background="#121826" BorderBrush="#1F293D" BorderThickness="0,0,0,1">
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <Border x:Name="Step1Dot" Width="24" Height="24" CornerRadius="12" Background="#FF5E3A">
                            <TextBlock x:Name="Step1Num" Text="1" FontSize="11" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <TextBlock x:Name="Step1Lbl" Text="Início" FontSize="12" FontWeight="SemiBold" Foreground="White" Margin="7,0,0,0" VerticalAlignment="Center"/>
                    </StackPanel>
                    <Border Width="28" Height="2" Background="#28354D" Margin="10,0"/>
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <Border x:Name="Step2Dot" Width="24" Height="24" CornerRadius="12" Background="#1E283D">
                            <TextBlock x:Name="Step2Num" Text="2" FontSize="11" FontWeight="Bold" Foreground="#94A3B8" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <TextBlock x:Name="Step2Lbl" Text="Playnite" FontSize="12" FontWeight="Medium" Foreground="#64748B" Margin="7,0,0,0" VerticalAlignment="Center"/>
                    </StackPanel>
                    <Border Width="28" Height="2" Background="#28354D" Margin="10,0"/>
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <Border x:Name="Step3Dot" Width="24" Height="24" CornerRadius="12" Background="#1E283D">
                            <TextBlock x:Name="Step3Num" Text="3" FontSize="11" FontWeight="Bold" Foreground="#94A3B8" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <TextBlock x:Name="Step3Lbl" Text="Pacotes" FontSize="12" FontWeight="Medium" Foreground="#64748B" Margin="7,0,0,0" VerticalAlignment="Center"/>
                    </StackPanel>
                    <Border Width="28" Height="2" Background="#28354D" Margin="10,0"/>
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <Border x:Name="Step4Dot" Width="24" Height="24" CornerRadius="12" Background="#1E283D">
                            <TextBlock x:Name="Step4Num" Text="4" FontSize="11" FontWeight="Bold" Foreground="#94A3B8" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <TextBlock x:Name="Step4Lbl" Text="Instalação" FontSize="12" FontWeight="Medium" Foreground="#64748B" Margin="7,0,0,0" VerticalAlignment="Center"/>
                    </StackPanel>
                    <Border Width="28" Height="2" Background="#28354D" Margin="10,0"/>
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                        <Border x:Name="Step5Dot" Width="24" Height="24" CornerRadius="12" Background="#1E283D">
                            <TextBlock x:Name="Step5Num" Text="5" FontSize="11" FontWeight="Bold" Foreground="#94A3B8" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <TextBlock x:Name="Step5Lbl" Text="Concluir" FontSize="12" FontWeight="Medium" Foreground="#64748B" Margin="7,0,0,0" VerticalAlignment="Center"/>
                    </StackPanel>
                </StackPanel>
            </Border>

            <!-- 3. ÁREA DE CONTEÚDO -->
            <Grid Grid.Row="2" Margin="28,18,28,14">

                <!-- TELA 1: APRESENTAÇÃO ESCALÁVEL -->
                <Grid x:Name="Page1" Visibility="Visible">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,12">
                        <Border Width="56" Height="56" CornerRadius="12" Background="#1A2236" BorderBrush="#2D3B59" BorderThickness="1">
                            <Grid>
                                <Image x:Name="WelcomeLogo" Width="38" Height="38"/>
                                <Viewbox x:Name="WelcomeFallbackLogo" Width="30" Height="30" Visibility="Collapsed">
                                    <Path Stretch="Uniform" Fill="#FF5E3A" Data="M7.5 6C4.5 6 2 8.8 2 12.8c0 2.7 1.1 5.2 2.8 5.2 1.2 0 2-1.2 3.1-2.5.6-.7 1.3-1 2.1-1h4c.8 0 1.5.3 2.1 1 1.1 1.3 1.9 2.5 3.1 2.5 1.7 0 2.8-2.5 2.8-5.2C22 8.8 19.5 6 16.5 6c-1.5 0-2.5.7-4.5.7S9 6 7.5 6Z"/>
                                </Viewbox>
                            </Grid>
                        </Border>
                        <StackPanel Margin="16,0,0,0" VerticalAlignment="Center">
                            <TextBlock Text="Bem-vindo ao Assistente de Instalação" FontSize="20" FontWeight="Bold" Foreground="White"/>
                            <TextBlock x:Name="TxtWelcomeSub" Text="Suite Playnite Extensions &amp; Themes" FontSize="12.5" Foreground="#94A3B8" Margin="0,3,0,0"/>
                        </StackPanel>
                    </StackPanel>

                    <TextBlock Grid.Row="1" TextWrapping="Wrap" FontSize="13" Foreground="#CBD5E1" Margin="0,0,0,12">
                        Este assistente instala e atualiza suas extensões e customizações visuais diretamente da release oficial. Confira os pacotes disponíveis:
                    </TextBlock>

                    <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto" Padding="0,0,6,0">
                        <ItemsControl x:Name="WelcomeItemsControl">
                            <ItemsControl.ItemsPanel>
                                <ItemsPanelTemplate>
                                    <UniformGrid Columns="2" VerticalAlignment="Top"/>
                                </ItemsPanelTemplate>
                            </ItemsControl.ItemsPanel>
                        </ItemsControl>
                    </ScrollViewer>

                    <Border Grid.Row="3" Background="#131E33" BorderBrush="#23375C" BorderThickness="1" CornerRadius="6" Padding="12,9" Margin="0,12,0,0">
                        <StackPanel Orientation="Horizontal">
                            <Border Width="18" Height="18" CornerRadius="9" Background="#1E3A5F" Margin="0,0,9,0" VerticalAlignment="Center">
                                <TextBlock Text="i" FontSize="11" FontWeight="Bold" Foreground="#60A5FA" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                            <TextBlock Text="Na próxima etapa você escolherá a pasta do Playnite e poderá selecionar exatamente o que deseja instalar."
                                       FontSize="11.5" Foreground="#93C5FD" TextWrapping="Wrap" VerticalAlignment="Center"/>
                        </StackPanel>
                    </Border>
                </Grid>

                <!-- TELA 2: LOCALIZAR PLAYNITE -->
                <Grid x:Name="Page2" Visibility="Collapsed">
                    <StackPanel VerticalAlignment="Top">
                        <TextBlock Text="Localizar Instalação do Playnite" FontSize="19" FontWeight="Bold" Foreground="White"/>
                        <TextBlock Text="Informe a pasta onde o Playnite está instalado (comum ou versão Portable)." FontSize="13" Foreground="#94A3B8" Margin="0,4,0,20"/>

                        <Border Background="{StaticResource BgCard}" BorderBrush="{StaticResource BorderSubtle}" BorderThickness="1" CornerRadius="8" Padding="18">
                            <StackPanel>
                                <TextBlock Text="Diretório do Playnite" FontSize="12" FontWeight="SemiBold" Foreground="#CBD5E1" Margin="0,0,0,8"/>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="10"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>
                                    <TextBox x:Name="TxtPlaynitePath" Grid.Column="0" Height="38" Padding="10,0"
                                             VerticalContentAlignment="Center" FontSize="12.5"
                                             Background="#0B101A" Foreground="White" BorderBrush="#334155" BorderThickness="1" CaretBrush="White"/>
                                    <Button x:Name="BtnBrowsePath" Grid.Column="2" Style="{StaticResource SecondaryBtn}" Height="38">
                                        <StackPanel Orientation="Horizontal">
                                            <Viewbox Width="15" Height="15" Margin="0,0,8,0">
                                                <Path Stretch="Uniform" Data="M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z" Fill="#FBBF24"/>
                                            </Viewbox>
                                            <TextBlock Text="Procurar Pasta..." VerticalAlignment="Center"/>
                                        </StackPanel>
                                    </Button>
                                </Grid>
                                <StackPanel Orientation="Horizontal" Margin="0,12,0,0">
                                    <Button x:Name="BtnAutoDetect" Style="{StaticResource SecondaryBtn}" Padding="12,6" FontSize="12">
                                        <StackPanel Orientation="Horizontal">
                                            <Viewbox Width="13" Height="13" Margin="0,0,7,0">
                                                <Path Stretch="Uniform" Data="M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5C7,5 5,7 5,9.5C5,12 7,14 9.5,14C12,14 14,12 14,9.5C14,7 12,5 9.5,5Z" Fill="#38BDF8"/>
                                            </Viewbox>
                                            <TextBlock Text="Detectar Automaticamente" VerticalAlignment="Center"/>
                                        </StackPanel>
                                    </Button>
                                </StackPanel>
                            </StackPanel>
                        </Border>

                        <Border x:Name="PathStatusBorder" Margin="0,16,0,0" CornerRadius="8" Padding="14,12" BorderThickness="1"
                                Background="#0D2920" BorderBrush="#10B981">
                            <StackPanel Orientation="Horizontal">
                                <Border x:Name="PathIconBadge" Width="30" Height="30" CornerRadius="15" Background="#10B981" VerticalAlignment="Center" Margin="0,0,12,0">
                                    <Viewbox Width="15" Height="15" HorizontalAlignment="Center" VerticalAlignment="Center">
                                        <Path x:Name="PathStatusVector" Stretch="Uniform" Data="M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z" Fill="White"/>
                                    </Viewbox>
                                </Border>
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock x:Name="PathStatusTitle" Text="Playnite Desktop localizado com sucesso!" FontSize="13" FontWeight="SemiBold" Foreground="#6EE7B7"/>
                                    <TextBlock x:Name="PathStatusSub" Text="Executável Playnite.DesktopApp.exe verificado nesta pasta." FontSize="11.5" Foreground="#A7F3D0" Margin="0,2,0,0"/>
                                </StackPanel>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </Grid>

                <!-- TELA 3: SELECIONAR COMPONENTES -->
                <Grid x:Name="Page3" Visibility="Collapsed">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <Grid Grid.Row="0" Margin="0,0,0,10">
                        <StackPanel>
                            <TextBlock Text="Selecione o que deseja instalar" FontSize="19" FontWeight="Bold" Foreground="White"/>
                            <TextBlock x:Name="TxtSelectSub" Text="3 de 3 pacotes selecionados" FontSize="12.5" Foreground="#94A3B8" Margin="0,2,0,0"/>
                        </StackPanel>
                        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Bottom">
                            <Button x:Name="BtnSelectAll" Content="Marcar Todos" Style="{StaticResource GhostBtn}"/>
                            <Button x:Name="BtnUnselectAll" Content="Desmarcar Todos" Style="{StaticResource GhostBtn}"/>
                        </StackPanel>
                    </Grid>

                    <Border Grid.Row="1" Background="#121826" BorderBrush="#28354D" BorderThickness="1" CornerRadius="6" Padding="10,0" Margin="0,0,0,12" Height="34">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <Viewbox Grid.Column="0" Width="13" Height="13" Margin="0,0,8,0" VerticalAlignment="Center">
                                <Path Stretch="Uniform" Data="M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.44,13.73L14.71,14H15.5L20.5,19L19,20.5L14,15.5V14.71L13.73,14.44C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5C7,5 5,7 5,9.5C5,12 7,14 9.5,14C12,14 14,12 14,9.5C14,7 12,5 9.5,5Z" Fill="#64748B"/>
                            </Viewbox>
                            <TextBlock Grid.Column="1" x:Name="SearchPlaceholder" Text="Filtrar por nome, tipo ou recurso..." Foreground="#64748B" FontSize="12" VerticalAlignment="Center" IsHitTestVisible="False"/>
                            <TextBox Grid.Column="1" x:Name="TxtSearchFilter" Background="Transparent" BorderThickness="0" Foreground="White" CaretBrush="White" FontSize="12" VerticalContentAlignment="Center"/>
                        </Grid>
                    </Border>

                    <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto" Padding="0,0,6,0">
                        <StackPanel x:Name="SelectionStackPanel"/>
                    </ScrollViewer>
                </Grid>

                <!-- TELA 4: PROGRESSO REAL -->
                <Grid x:Name="Page4" Visibility="Collapsed">
                    <StackPanel VerticalAlignment="Center">
                        <TextBlock x:Name="TxtInstallTitle" Text="Instalando pacotes selecionados..." FontSize="19" FontWeight="Bold" Foreground="White"/>
                        <TextBlock x:Name="TxtInstallStep" Text="Preparando instalação..." FontSize="13" Foreground="#38BDF8" Margin="0,4,0,18"/>

                        <Grid Margin="0,0,0,16">
                            <ProgressBar x:Name="InstallProgressBar" Height="16" Minimum="0" Maximum="100" Value="0"
                                         Background="#182032" Foreground="#10B981" BorderBrush="#28354D" BorderThickness="1">
                                <ProgressBar.Template>
                                    <ControlTemplate TargetType="ProgressBar">
                                        <Grid>
                                            <Border x:Name="PART_Track" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"/>
                                            <Border x:Name="PART_Indicator" HorizontalAlignment="Left" CornerRadius="8" Background="{TemplateBinding Foreground}"/>
                                        </Grid>
                                    </ControlTemplate>
                                </ProgressBar.Template>
                            </ProgressBar>
                            <TextBlock x:Name="TxtProgressPct" Text="0%" FontSize="10" FontWeight="Bold" Foreground="White" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Grid>

                        <Border Background="#080B12" BorderBrush="#1E293B" BorderThickness="1" CornerRadius="8" Padding="12" Height="200">
                            <ScrollViewer x:Name="LogScrollViewer" VerticalScrollBarVisibility="Auto">
                                <TextBlock x:Name="TxtInstallLog" FontFamily="Consolas" FontSize="11.5" Foreground="#94A3B8" TextWrapping="Wrap"/>
                            </ScrollViewer>
                        </Border>
                    </StackPanel>
                </Grid>

                <!-- TELA 5: CONCLUSÃO -->
                <Grid x:Name="Page5" Visibility="Collapsed">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,16">
                        <Border x:Name="FinalBadge" Width="52" Height="52" CornerRadius="26" Background="#0D2920" BorderBrush="#10B981" BorderThickness="2">
                            <Viewbox Width="24" Height="24" HorizontalAlignment="Center" VerticalAlignment="Center">
                                <Path x:Name="FinalVector" Stretch="Uniform" Data="M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z" Fill="#10B981"/>
                            </Viewbox>
                        </Border>
                        <StackPanel Margin="16,0,0,0" VerticalAlignment="Center">
                            <TextBlock x:Name="TxtFinalTitle" Text="Instalação Concluída com Sucesso!" FontSize="20" FontWeight="Bold" Foreground="White"/>
                            <TextBlock x:Name="TxtFinalSub" Text="Todos os itens selecionados foram instalados no seu Playnite." FontSize="13" Foreground="#6EE7B7" Margin="0,3,0,0"/>
                        </StackPanel>
                    </StackPanel>

                    <Border Grid.Row="1" Background="{StaticResource BgCard}" BorderBrush="{StaticResource BorderSubtle}" BorderThickness="1" CornerRadius="8" Padding="16" Margin="0,0,0,16">
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="*"/>
                            </Grid.RowDefinitions>
                            <TextBlock Grid.Row="0" Text="RESUMO DA INSTALAÇÃO" FontSize="11" FontWeight="Bold" Foreground="#64748B" Margin="0,0,0,6"/>
                            <TextBlock Grid.Row="1" x:Name="TxtSummaryPath" Text="Destino: ..." FontSize="12.5" Foreground="#CBD5E1" Margin="0,0,0,8"/>
                            <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto">
                                <StackPanel x:Name="SummaryItemsPanel"/>
                            </ScrollViewer>
                        </Grid>
                    </Border>

                    <CheckBox Grid.Row="2" x:Name="ChkLaunchPlaynite" IsChecked="True" Style="{StaticResource ModernCheckBox}">
                        <TextBlock Text="Abrir o Playnite Desktop ao concluir" FontSize="13" Foreground="#E2E8F0"/>
                    </CheckBox>
                </Grid>
            </Grid>

            <!-- 4. RODAPÉ -->
            <Border Grid.Row="3" Background="#121826" CornerRadius="0,0,12,12" BorderBrush="#1F293D" BorderThickness="0,1,0,0">
                <Grid Margin="24,0">
                    <Button x:Name="BtnCancel" Content="Cancelar" Style="{StaticResource GhostBtn}" HorizontalAlignment="Left" VerticalAlignment="Center"/>
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
                        <Button x:Name="BtnBack" Content="← Voltar" Style="{StaticResource SecondaryBtn}" Margin="0,0,10,0" Visibility="Collapsed"/>
                        <Button x:Name="BtnNext" Content="Avançar →" Style="{StaticResource PrimaryBtn}"/>
                    </StackPanel>
                </Grid>
            </Border>
        </Grid>
    </Border>
</Window>
'@

$reader = New-Object System.Xml.XmlNodeReader $xaml
$window = [Windows.Markup.XamlReader]::Load($reader)

# Controles
$TitleBar            = $window.FindName("TitleBar")
$BtnMinimize         = $window.FindName("BtnMinimize")
$BtnClose            = $window.FindName("BtnClose")
$HeaderLogo          = $window.FindName("HeaderLogo")
$WelcomeLogo         = $window.FindName("WelcomeLogo")
$WelcomeFallbackLogo = $window.FindName("WelcomeFallbackLogo")
$ReleaseTagBadge     = $window.FindName("ReleaseTagBadge")
$TxtReleaseTag       = $window.FindName("TxtReleaseTag")
$TxtWelcomeSub       = $window.FindName("TxtWelcomeSub")
$WelcomeItemsControl = $window.FindName("WelcomeItemsControl")

$Pages = @($window.FindName("Page1"), $window.FindName("Page2"), $window.FindName("Page3"), $window.FindName("Page4"), $window.FindName("Page5"))
$StepDots = @($window.FindName("Step1Dot"), $window.FindName("Step2Dot"), $window.FindName("Step3Dot"), $window.FindName("Step4Dot"), $window.FindName("Step5Dot"))
$StepNums = @($window.FindName("Step1Num"), $window.FindName("Step2Num"), $window.FindName("Step3Num"), $window.FindName("Step4Num"), $window.FindName("Step5Num"))
$StepLbls = @($window.FindName("Step1Lbl"), $window.FindName("Step2Lbl"), $window.FindName("Step3Lbl"), $window.FindName("Step4Lbl"), $window.FindName("Step5Lbl"))

$BtnCancel = $window.FindName("BtnCancel")
$BtnBack   = $window.FindName("BtnBack")
$BtnNext   = $window.FindName("BtnNext")

# Tela 2
$TxtPlaynitePath    = $window.FindName("TxtPlaynitePath")
$BtnBrowsePath      = $window.FindName("BtnBrowsePath")
$BtnAutoDetect      = $window.FindName("BtnAutoDetect")
$PathStatusBorder   = $window.FindName("PathStatusBorder")
$PathIconBadge      = $window.FindName("PathIconBadge")
$PathStatusVector   = $window.FindName("PathStatusVector")
$PathStatusTitle    = $window.FindName("PathStatusTitle")
$PathStatusSub      = $window.FindName("PathStatusSub")

# Tela 3
$TxtSelectSub        = $window.FindName("TxtSelectSub")
$BtnSelectAll        = $window.FindName("BtnSelectAll")
$BtnUnselectAll      = $window.FindName("BtnUnselectAll")
$TxtSearchFilter     = $window.FindName("TxtSearchFilter")
$SearchPlaceholder   = $window.FindName("SearchPlaceholder")
$SelectionStackPanel = $window.FindName("SelectionStackPanel")

# Tela 4
$TxtInstallTitle     = $window.FindName("TxtInstallTitle")
$TxtInstallStep      = $window.FindName("TxtInstallStep")
$InstallProgressBar  = $window.FindName("InstallProgressBar")
$TxtProgressPct      = $window.FindName("TxtProgressPct")
$TxtInstallLog       = $window.FindName("TxtInstallLog")
$LogScrollViewer     = $window.FindName("LogScrollViewer")

# Tela 5
$FinalBadge        = $window.FindName("FinalBadge")
$FinalVector       = $window.FindName("FinalVector")
$TxtFinalTitle     = $window.FindName("TxtFinalTitle")
$TxtFinalSub       = $window.FindName("TxtFinalSub")
$TxtSummaryPath    = $window.FindName("TxtSummaryPath")
$SummaryItemsPanel = $window.FindName("SummaryItemsPanel")
$ChkLaunchPlaynite = $window.FindName("ChkLaunchPlaynite")

function Get-Brush([string]$hex) { return (New-Object System.Windows.Media.BrushConverter).ConvertFromString($hex) }

function Load-AppLogo {
    try {
        $localLogo = if ($scriptRoot) { Join-Path $scriptRoot "assets\playnite-logo.png" } else { "" }
        $bmp = New-Object System.Windows.Media.Imaging.BitmapImage
        if ($localLogo -and (Test-Path $localLogo)) {
            $bmp.BeginInit()
            $bmp.UriSource = New-Object Uri($localLogo)
            $bmp.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
            $bmp.EndInit()
        } else {
            $logoUrl = "$RawBaseUrl/assets/playnite-logo.png"
            if (-not (Test-SafeGitHubUrl $logoUrl)) { throw "URL inválida" }
            $wc = New-Object System.Net.WebClient
            $bytes = $wc.DownloadData($logoUrl)
            $ms = New-Object System.IO.MemoryStream(, $bytes)
            $bmp.BeginInit()
            $bmp.StreamSource = $ms
            $bmp.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
            $bmp.EndInit()
        }
        $HeaderLogo.Source = $bmp
        $WelcomeLogo.Source = $bmp
    } catch {
        $WelcomeLogo.Visibility = "Collapsed"
        $WelcomeFallbackLogo.Visibility = "Visible"
    }
}

function New-VectorIconBadge([string]$pathData, [string]$iconColor, [string]$bgColor, [int]$size = 28) {
    $border = New-Object System.Windows.Controls.Border
    $border.Width = $size; $border.Height = $size
    $border.CornerRadius = New-Object System.Windows.CornerRadius(7)
    $border.Background = Get-Brush $bgColor
    $border.BorderBrush = Get-Brush $iconColor
    $border.BorderThickness = New-Object System.Windows.Thickness(1)
    $border.VerticalAlignment = "Center"

    $vb = New-Object System.Windows.Controls.Viewbox
    $vb.Width = [int]($size * 0.56); $vb.Height = [int]($size * 0.56)
    $vb.HorizontalAlignment = "Center"; $vb.VerticalAlignment = "Center"
    $path = New-Object System.Windows.Shapes.Path
    $path.Stretch = [System.Windows.Media.Stretch]::Uniform
    $path.Data = [System.Windows.Media.Geometry]::Parse($pathData)
    $path.Fill = Get-Brush $iconColor
    $vb.Child = $path
    $border.Child = $vb
    return $border
}

$script:Catalog = Load-CatalogItems $scriptRoot
$script:LatestRelease = $null
try {
    $script:LatestRelease = Invoke-RestMethod -Uri $LatestReleaseApi -UseBasicParsing -TimeoutSec 6
    if ($script:LatestRelease -and $script:LatestRelease.tag_name) {
        $TxtReleaseTag.Text = "RELEASE $($script:LatestRelease.tag_name)"
        $ReleaseTagBadge.Visibility = "Visible"

        # Atualiza versões exibidas dinamicamente a partir dos nomes dos .pext na release
        foreach ($item in $script:Catalog) {
            $asset = $script:LatestRelease.assets | Where-Object { $_.name -like $item.AssetPattern } | Select-Object -First 1
            if ($asset -and $asset.name -match '_(\d+(?:_\d+)+)\.pext$') {
                $item.Version = "v" + ($Matches[1] -replace '_', '.')
            }
        }
    }
} catch {}

$script:CurrentPage = 1
$script:PathIsValid = $false
$script:LastCheckedPlaynitePath = ""
$script:WorkerTimer = $null
$script:InstallSuccess = $true

function Compare-SafeVersion([string]$v1, [string]$v2) {
    $c1 = ($v1 -replace '^v', '' -replace '-.*$', '').Trim()
    $c2 = ($v2 -replace '^v', '' -replace '-.*$', '').Trim()
    try {
        $ver1 = [Version]$c1
        $ver2 = [Version]$c2
        return $ver1.CompareTo($ver2)
    } catch {
        return [string]::Compare($c1, $c2, [System.StringComparison]::OrdinalIgnoreCase)
    }
}

function Update-InstalledStatuses([string]$playniteDir) {
    foreach ($item in $script:Catalog) {
        $item.InstallStatus = "NotInstalled"
        $item.StatusLabel = "NÃO INSTALADO"
        $item.StatusBg = "#1E293B"
        $item.StatusFg = "#94A3B8"
        $item.ActionWord = "Instalado"
        $item.Selected = [bool]$item.DefaultSelected

        if (-not $playniteDir -or -not (Test-Path $playniteDir)) { continue }

        $destDir = Join-Path $playniteDir $item.InstallSubdir
        $catalogVerClean = ($item.Version -replace '^v', '').Trim()

        if ($item.Type -like "*TEMA*") {
            $themeYaml = Join-Path $destDir "theme.yaml"
            if (Test-Path $themeYaml) {
                $m = Select-String -Path $themeYaml -Pattern "^Version:\s*(.+)$" | Select-Object -First 1
                $instVer = if ($m) { $m.Matches.Groups[1].Value.Trim() } else { "?" }
                $tplFile = Join-Path $destDir "DerivedStyles\GridViewItemTemplate.xaml"
                $hasMod = (Test-Path $tplFile) -and (Select-String -Path $tplFile -Pattern "ActiveRunningBadge" -Quiet)

                if ($hasMod) {
                    $cmp = Compare-SafeVersion $instVer $catalogVerClean
                    if ($cmp -lt 0) {
                        $item.InstallStatus = "Update"
                        $item.StatusLabel = "ATUALIZAR (v$instVer → $($item.Version))"
                        $item.StatusBg = "#122B40"; $item.StatusFg = "#38BDF8"
                        $item.ActionWord = "Atualizado"
                        $item.Selected = $true
                    } else {
                        $item.InstallStatus = "Installed"
                        $item.StatusLabel = "JÁ INSTALADO (v$instVer-mod)"
                        $item.StatusBg = "#0D2920"; $item.StatusFg = "#34D399"
                        $item.ActionWord = "Reinstalado"
                        $item.Selected = $false
                    }
                } else {
                    $item.InstallStatus = "Update"
                    $item.StatusLabel = "ORIGINAL DETECTADO - ATUALIZAR"
                    $item.StatusBg = "#2D2212"; $item.StatusFg = "#FBBF24"
                    $item.ActionWord = "Atualizado"
                    $item.Selected = $true
                }
            }
        } else {
            $extYaml = Join-Path $destDir "extension.yaml"
            if (Test-Path $extYaml) {
                $m = Select-String -Path $extYaml -Pattern "^Version:\s*(.+)$" | Select-Object -First 1
                $instVer = if ($m) { $m.Matches.Groups[1].Value.Trim() } else { "?" }
                $cmp = Compare-SafeVersion $instVer $catalogVerClean
                if ($cmp -lt 0) {
                    $item.InstallStatus = "Update"
                    $item.StatusLabel = "ATUALIZAR (v$instVer → $($item.Version))"
                    $item.StatusBg = "#122B40"; $item.StatusFg = "#38BDF8"
                    $item.ActionWord = "Atualizado"
                    $item.Selected = $true
                } else {
                    $item.InstallStatus = "Installed"
                    $item.StatusLabel = "JÁ INSTALADO (v$instVer)"
                    $item.StatusBg = "#0D2920"; $item.StatusFg = "#34D399"
                    $item.ActionWord = "Reinstalado"
                    $item.Selected = $false
                }
            }
        }
    }
}

function Render-WelcomeCards {
    $WelcomeItemsControl.Items.Clear()
    $TxtWelcomeSub.Text = "Suite Playnite Extensions & Themes • $($script:Catalog.Count) pacotes disponíveis"

    foreach ($item in $script:Catalog) {
        $border = New-Object System.Windows.Controls.Border
        $border.Background = Get-Brush "#182032"
        $border.BorderBrush = Get-Brush "#28354D"
        $border.BorderThickness = New-Object System.Windows.Thickness(1)
        $border.CornerRadius = New-Object System.Windows.CornerRadius(8)
        $border.Padding = New-Object System.Windows.Thickness(12)
        $border.Margin = New-Object System.Windows.Thickness(0,0,10,10)

        $sp = New-Object System.Windows.Controls.StackPanel
        $hdr = New-Object System.Windows.Controls.DockPanel
        $hdr.LastChildFill = $true

        $iconBadge = New-VectorIconBadge $item.IconPath $item.Accent $item.IconBg 26
        $iconBadge.Margin = New-Object System.Windows.Thickness(0,0,9,0)
        [System.Windows.Controls.DockPanel]::SetDock($iconBadge, "Left")

        $badge = New-Object System.Windows.Controls.Border
        $badge.Background = Get-Brush $item.BadgeBg
        $badge.CornerRadius = New-Object System.Windows.CornerRadius(4)
        $badge.Padding = New-Object System.Windows.Thickness(5,1,5,1)
        $badge.VerticalAlignment = "Center"
        [System.Windows.Controls.DockPanel]::SetDock($badge, "Right")
        $bTxt = New-Object System.Windows.Controls.TextBlock
        $bTxt.Text = $item.Version; $bTxt.FontSize = 9.5; $bTxt.FontWeight = "Bold"; $bTxt.Foreground = Get-Brush $item.BadgeFg
        $badge.Child = $bTxt

        $title = New-Object System.Windows.Controls.TextBlock
        $title.Text = $item.Name
        $title.FontSize = 12.5; $title.FontWeight = "Bold"; $title.Foreground = Get-Brush "#F8FAFC"
        $title.TextTrimming = "CharacterEllipsis"; $title.VerticalAlignment = "Center"

        $hdr.Children.Add($iconBadge) | Out-Null
        $hdr.Children.Add($badge) | Out-Null
        $hdr.Children.Add($title) | Out-Null

        $desc = New-Object System.Windows.Controls.TextBlock
        $desc.Text = $item.Short
        $desc.FontSize = 11.5; $desc.Foreground = Get-Brush "#94A3B8"; $desc.TextWrapping = "Wrap"
        $desc.Margin = New-Object System.Windows.Thickness(0,8,0,0)

        $sp.Children.Add($hdr) | Out-Null
        $sp.Children.Add($desc) | Out-Null
        $border.Child = $sp
        $WelcomeItemsControl.Items.Add($border) | Out-Null
    }
}

function Update-SelectionSummary {
    $selectedCount = ($script:Catalog | Where-Object { $_.Selected }).Count
    $totalCount = $script:Catalog.Count
    $allInstalled = ($script:Catalog | Where-Object { $_.InstallStatus -ne "Installed" }).Count -eq 0
    if ($allInstalled -and $selectedCount -eq 0) {
        $TxtSelectSub.Text = "Todos os $totalCount pacotes já estão instalados e atualizados (marque para reinstalar)"
    } else {
        $TxtSelectSub.Text = "$selectedCount de $totalCount pacotes selecionados"
    }
    if ($script:CurrentPage -eq 3) {
        $BtnNext.IsEnabled = ($selectedCount -gt 0)
        $BtnNext.Content = if ($selectedCount -gt 0) { "Instalar ($selectedCount) →" } else { "Selecione ao menos 1" }
    }
}

function Test-IsInsideCheckBox($obj) {
    $curr = $obj -as [System.Windows.DependencyObject]
    while ($curr -ne $null) {
        if ($curr -is [System.Windows.Controls.CheckBox]) { return $true }
        $curr = [System.Windows.Media.VisualTreeHelper]::GetParent($curr)
    }
    return $false
}

function Render-SelectionCards {
    $SelectionStackPanel.Children.Clear()
    $filter = $TxtSearchFilter.Text.Trim().ToLower()
    $SearchPlaceholder.Visibility = if ($TxtSearchFilter.Text.Length -gt 0) { "Collapsed" } else { "Visible" }

    foreach ($item in $script:Catalog) {
        if ($filter -and -not ($item.Name.ToLower().Contains($filter) -or $item.Type.ToLower().Contains($filter) -or $item.Short.ToLower().Contains($filter))) {
            continue
        }

        $card = New-Object System.Windows.Controls.Border
        $card.BorderThickness = New-Object System.Windows.Thickness(1)
        $card.CornerRadius = New-Object System.Windows.CornerRadius(8)
        $card.Padding = New-Object System.Windows.Thickness(14,11,14,11)
        $card.Margin = New-Object System.Windows.Thickness(0,0,0,9)
        $card.Cursor = [System.Windows.Input.Cursors]::Hand
        $card.Background = if ($item.Selected) { Get-Brush "#182436" } else { Get-Brush "#141B29" }
        $card.BorderBrush = if ($item.Selected) { Get-Brush "#10B981" } else { Get-Brush "#28354D" }

        $chk = New-Object System.Windows.Controls.CheckBox
        $chk.Style = $window.FindResource("ModernCheckBox")
        $chk.IsChecked = $item.Selected

        $rowGrid = New-Object System.Windows.Controls.StackPanel
        $rowGrid.Orientation = "Horizontal"

        $iconBadge = New-VectorIconBadge $item.IconPath $item.Accent $item.IconBg 34
        $iconBadge.Margin = New-Object System.Windows.Thickness(0,0,12,0)

        $contentSp = New-Object System.Windows.Controls.StackPanel
        $contentSp.VerticalAlignment = "Center"
        $topRow = New-Object System.Windows.Controls.StackPanel
        $topRow.Orientation = "Horizontal"

        $nameLbl = New-Object System.Windows.Controls.TextBlock
        $nameLbl.Text = $item.Name; $nameLbl.FontSize = 13.5; $nameLbl.FontWeight = "Bold"; $nameLbl.Foreground = Get-Brush "#FFFFFF"

        $typeBadge = New-Object System.Windows.Controls.Border
        $typeBadge.Background = Get-Brush $item.BadgeBg; $typeBadge.CornerRadius = New-Object System.Windows.CornerRadius(4)
        $typeBadge.Padding = New-Object System.Windows.Thickness(6,2,6,2); $typeBadge.Margin = New-Object System.Windows.Thickness(10,0,0,0)
        $tTxt = New-Object System.Windows.Controls.TextBlock
        $tTxt.Text = $item.Type; $tTxt.FontSize = 9; $tTxt.FontWeight = "Bold"; $tTxt.Foreground = Get-Brush $item.BadgeFg
        $typeBadge.Child = $tTxt

        $verBadge = New-Object System.Windows.Controls.Border
        $verBadge.Background = Get-Brush "#0D2920"; $verBadge.CornerRadius = New-Object System.Windows.CornerRadius(4)
        $verBadge.Padding = New-Object System.Windows.Thickness(6,2,6,2); $verBadge.Margin = New-Object System.Windows.Thickness(6,0,0,0)
        $vTxt = New-Object System.Windows.Controls.TextBlock
        $vTxt.Text = $item.Version; $vTxt.FontSize = 9; $vTxt.FontWeight = "Bold"; $vTxt.Foreground = Get-Brush "#34D399"
        $verBadge.Child = $vTxt

        $statusBadge = New-Object System.Windows.Controls.Border
        $statusBadge.Background = Get-Brush $item.StatusBg; $statusBadge.CornerRadius = New-Object System.Windows.CornerRadius(4)
        $statusBadge.Padding = New-Object System.Windows.Thickness(6,2,6,2); $statusBadge.Margin = New-Object System.Windows.Thickness(6,0,0,0)
        $sTxt = New-Object System.Windows.Controls.TextBlock
        $sTxt.Text = $item.StatusLabel; $sTxt.FontSize = 9; $sTxt.FontWeight = "Bold"; $sTxt.Foreground = Get-Brush $item.StatusFg
        $statusBadge.Child = $sTxt

        $topRow.Children.Add($nameLbl) | Out-Null
        $topRow.Children.Add($typeBadge) | Out-Null
        $topRow.Children.Add($verBadge) | Out-Null
        $topRow.Children.Add($statusBadge) | Out-Null

        $descLbl = New-Object System.Windows.Controls.TextBlock
        $descLbl.Text = $item.Short; $descLbl.FontSize = 11.5; $descLbl.Foreground = Get-Brush "#94A3B8"
        $descLbl.TextWrapping = "Wrap"; $descLbl.Margin = New-Object System.Windows.Thickness(0,4,0,0)

        $contentSp.Children.Add($topRow) | Out-Null
        $contentSp.Children.Add($descLbl) | Out-Null
        $rowGrid.Children.Add($iconBadge) | Out-Null
        $rowGrid.Children.Add($contentSp) | Out-Null

        $chk.Content = $rowGrid
        $card.Child = $chk

        $ctx = @{ Item = $item; Card = $card; Chk = $chk }
        $chk.Tag = $ctx; $card.Tag = $ctx

        $chk.Add_Click({
            $c = $this.Tag
            $c.Item.Selected = [bool]$c.Chk.IsChecked
            $c.Card.Background = if ($c.Item.Selected) { Get-Brush "#182436" } else { Get-Brush "#141B29" }
            $c.Card.BorderBrush = if ($c.Item.Selected) { Get-Brush "#10B981" } else { Get-Brush "#28354D" }
            Update-SelectionSummary
        })

        $card.Add_MouseLeftButtonUp({
            param($sender, $e)
            if (-not (Test-IsInsideCheckBox $e.OriginalSource)) {
                $c = $sender.Tag
                $c.Item.Selected = -not $c.Item.Selected
                $c.Chk.IsChecked = $c.Item.Selected
                $c.Card.Background = if ($c.Item.Selected) { Get-Brush "#182436" } else { Get-Brush "#141B29" }
                $c.Card.BorderBrush = if ($c.Item.Selected) { Get-Brush "#10B981" } else { Get-Brush "#28354D" }
                Update-SelectionSummary
            }
        })

        $SelectionStackPanel.Children.Add($card) | Out-Null
    }
    Update-SelectionSummary
}

function Update-PlayniteValidation {
    $path = $TxtPlaynitePath.Text.Trim()
    $exe = if ($path) { Join-Path $path "Playnite.DesktopApp.exe" } else { "" }

    if ($path -and (Test-Path $exe)) {
        $script:PathIsValid = $true
        $ver = (Get-Item $exe).VersionInfo.ProductVersion
        $PathStatusBorder.Background = Get-Brush "#0D2920"
        $PathStatusBorder.BorderBrush = Get-Brush "#10B981"
        $PathIconBadge.Background = Get-Brush "#10B981"
        $PathStatusVector.Data = [System.Windows.Media.Geometry]::Parse("M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z")
        $PathStatusVector.Fill = Get-Brush "#FFFFFF"
        $PathStatusTitle.Text = "Playnite Desktop encontrado com sucesso! (v$ver)"
        $PathStatusTitle.Foreground = Get-Brush "#6EE7B7"
        $PathStatusSub.Text = "Executável validado: $exe"
        $PathStatusSub.Foreground = Get-Brush "#A7F3D0"
    } else {
        $script:PathIsValid = $false
        $PathStatusBorder.Background = Get-Brush "#2D1E12"
        $PathStatusBorder.BorderBrush = Get-Brush "#F59E0B"
        $PathIconBadge.Background = Get-Brush "#F59E0B"
        $PathStatusVector.Data = [System.Windows.Media.Geometry]::Parse("M0,0 H3 V9 H0 Z M0,11 H3 V14 H0 Z")
        $PathStatusVector.Fill = Get-Brush "#111827"
        $PathStatusTitle.Text = "Playnite.DesktopApp.exe não encontrado nesta pasta"
        $PathStatusTitle.Foreground = Get-Brush "#FCD34D"
        $PathStatusSub.Text = "Use 'Procurar Pasta...' para apontar onde o Playnite está instalado."
        $PathStatusSub.Foreground = Get-Brush "#FDE68A"
    }
    if ($script:CurrentPage -eq 2) { $BtnNext.IsEnabled = $script:PathIsValid }
}

function Update-Stepper([int]$page) {
    for ($i = 0; $i -lt 5; $i++) {
        $stepIdx = $i + 1
        if ($stepIdx -lt $page) {
            $StepDots[$i].Background = Get-Brush "#10B981"; $StepNums[$i].Text = "✓"
            $StepNums[$i].Foreground = Get-Brush "#FFFFFF"; $StepLbls[$i].Foreground = Get-Brush "#A7F3D0"
        } elseif ($stepIdx -eq $page) {
            $StepDots[$i].Background = Get-Brush "#FF5E3A"; $StepNums[$i].Text = "$stepIdx"
            $StepNums[$i].Foreground = Get-Brush "#FFFFFF"; $StepLbls[$i].Foreground = Get-Brush "#FFFFFF"
        } else {
            $StepDots[$i].Background = Get-Brush "#1E283D"; $StepNums[$i].Text = "$stepIdx"
            $StepNums[$i].Foreground = Get-Brush "#94A3B8"; $StepLbls[$i].Foreground = Get-Brush "#64748B"
        }
    }
}

function Start-RealInstallation {
    $playniteDir = $TxtPlaynitePath.Text.Trim()
    $selectedItems = @($script:Catalog | Where-Object { $_.Selected })

    # Verifica se o Playnite está aberto antes de iniciar
    $procs = Get-Process -Name "*Playnite*" -ErrorAction SilentlyContinue
    if ($procs) {
        $ans = [System.Windows.MessageBox]::Show(
            "O Playnite está em execução no momento.`n`nDeseja encerrá-lo agora para atualizar os arquivos com segurança?",
            "Playnite em Execução",
            "YesNo",
            "Warning"
        )
        if ($ans -ne "Yes") {
            Go-ToPage 3
            return
        }
        $procs | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 800
    }

    $InstallProgressBar.Value = 5
    $TxtProgressPct.Text = "5%"
    $TxtInstallStep.Text = "Iniciando instalação..."
    $TxtInstallLog.Text = "Destino: $playniteDir`n"

    $sync = [hashtable]::Synchronized(@{
        Progress = 5
        Step     = "Conectando à release no GitHub..."
        Logs     = [System.Collections.ArrayList]::Synchronized((New-Object System.Collections.ArrayList))
        Done     = $false
        Error    = $null
        Results  = [System.Collections.ArrayList]::Synchronized((New-Object System.Collections.ArrayList))
    })

    $ps = [PowerShell]::Create()
    $ps.AddScript({
        param($sync, $selectedItems, $playniteDir, $scriptRoot, $latestReleaseApi, $cachedRelease)
        $ErrorActionPreference = "Stop"

        function Test-UrlSafe([string]$url) {
            try {
                $u = [Uri]$url
                if ($u.Scheme -ne "https") { return $false }
                $hosts = @("github.com", "raw.githubusercontent.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com")
                return ($hosts -contains $u.Host.ToLower())
            } catch { return $false }
        }

        function Extract-ZipSafe([string]$zipPath, [string]$destDir) {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $fullRoot = [System.IO.Path]::GetFullPath($destDir)
            if (-not $fullRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
                $fullRoot += [System.IO.Path]::DirectorySeparatorChar
            }
            New-Item -ItemType Directory -Path $fullRoot -Force | Out-Null
            $arch = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
            try {
                foreach ($entry in $arch.Entries) {
                    if ([string]::IsNullOrEmpty($entry.FullName)) { continue }
                    $norm = $entry.FullName -replace '/', '\'
                    $dp = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($fullRoot, $norm))
                    if (-not $dp.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                        throw "Entrada inválida bloqueada (Path Traversal): $($entry.FullName)"
                    }
                    if ($entry.FullName.EndsWith("/") -or $entry.FullName.EndsWith("\")) {
                        New-Item -ItemType Directory -Path $dp -Force | Out-Null
                    } else {
                        $par = [System.IO.Path]::GetDirectoryName($dp)
                        if ($par) { New-Item -ItemType Directory -Path $par -Force | Out-Null }
                        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dp, $true)
                    }
                }
            } finally {
                $arch.Dispose()
            }
        }

        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("PlayniteInst_" + [Guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        try {
            $release = $cachedRelease
            if (-not $release) {
                $sync.Logs.Add("[API] Consultando última release no GitHub...") | Out-Null
                $release = Invoke-RestMethod -Uri $latestReleaseApi -UseBasicParsing -TimeoutSec 15
            }
            $sync.Logs.Add("[API] Release encontrada: $($release.tag_name)") | Out-Null
            $sync.Progress = 15

            $idx = 0
            $total = $selectedItems.Count
            foreach ($item in $selectedItems) {
                $idx++
                $basePct = 15 + [int]((($idx - 1) / $total) * 80)
                $sync.Progress = $basePct
                $sync.Step = "Instalando $($item.Name) ($idx/$total)..."

                $destDir = Join-Path $playniteDir $item.InstallSubdir
                $localTheme = if ($scriptRoot -and $item.LocalThemeDir) { Join-Path $scriptRoot $item.LocalThemeDir } else { "" }

                if ($localTheme -and (Test-Path $localTheme)) {
                    $sync.Logs.Add("[$($item.Name)] Copiando arquivos locais do tema...") | Out-Null
                    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                    Copy-Item -Path "$localTheme\*" -Destination $destDir -Recurse -Force
                } else {
                    $localPext = $null
                    if ($scriptRoot -and (Test-Path "$scriptRoot\releases")) {
                        $localPext = Get-ChildItem "$scriptRoot\releases" -Filter $item.AssetPattern -ErrorAction SilentlyContinue | Select-Object -First 1
                    }
                    $pkgFile = Join-Path $tempDir "$($item.Id).pkg"

                    if ($localPext) {
                        $sync.Logs.Add("[$($item.Name)] Usando pacote local: $($localPext.Name)") | Out-Null
                        Copy-Item -Path $localPext.FullName -Destination $pkgFile -Force
                    } else {
                        $asset = $release.assets | Where-Object { $_.name -like $item.AssetPattern } | Select-Object -First 1
                        if (-not $asset) { throw "Pacote ($($item.AssetPattern)) não encontrado na release $($release.tag_name)." }
                        if (-not (Test-UrlSafe $asset.browser_download_url)) { throw "URL não permitida: $($asset.browser_download_url)" }

                        $sync.Logs.Add("[$($item.Name)] Baixando $($asset.name)...") | Out-Null
                        $wc = New-Object System.Net.WebClient
                        $wc.DownloadFile($asset.browser_download_url, $pkgFile)
                    }

                    $sync.Logs.Add("[$($item.Name)] Extraindo para $destDir...") | Out-Null
                    Extract-ZipSafe $pkgFile $destDir
                }

                $sync.Results.Add(@{ Item = $item; Ok = $true; Msg = "Instalado com sucesso" }) | Out-Null
                $sync.Logs.Add("[OK] $($item.Name) concluído!") | Out-Null
            }

            $sync.Progress = 100
            $sync.Step = "Instalação finalizada!"
            $sync.Logs.Add("[100%] Todos os $total componentes foram instalados no Playnite.") | Out-Null
        } catch {
            $sync.Error = $_.Exception.Message
            $sync.Logs.Add("[ERRO] $($sync.Error)") | Out-Null
        } finally {
            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
            $sync.Done = $true
        }
    }).AddArgument($sync).AddArgument($selectedItems).AddArgument($playniteDir).AddArgument($scriptRoot).AddArgument($LatestReleaseApi).AddArgument($script:LatestRelease) | Out-Null

    $asyncResult = $ps.BeginInvoke()
    $script:LastLogCount = 0

    $script:WorkerTimer = New-Object System.Windows.Threading.DispatcherTimer
    $script:WorkerTimer.Interval = [TimeSpan]::FromMilliseconds(120)
    $script:WorkerTimer.Tag = @{ Sync = $sync; PS = $ps; Async = $asyncResult }
    $script:WorkerTimer.Add_Tick({
        $ctx = $this.Tag
        $s = $ctx.Sync

        $InstallProgressBar.Value = $s.Progress
        $TxtProgressPct.Text = "$($s.Progress)%"
        $TxtInstallStep.Text = $s.Step

        while ($script:LastLogCount -lt $s.Logs.Count) {
            $TxtInstallLog.Text += "$($s.Logs[$script:LastLogCount])`n"
            $script:LastLogCount++
            $LogScrollViewer.ScrollToEnd()
        }

        if ($s.Done) {
            $this.Stop()
            try { $ctx.PS.EndInvoke($ctx.Async) } catch {}
            $ctx.PS.Dispose()

            $BtnCancel.IsEnabled = $true
            $script:InstallSuccess = [string]::IsNullOrEmpty($s.Error)
            $script:InstallErrorMsg = $s.Error
            Go-ToPage 5
        }
    })
    $script:WorkerTimer.Start()
}

function Go-ToPage([int]$page) {
    $script:CurrentPage = $page
    for ($i = 0; $i -lt $Pages.Count; $i++) {
        $Pages[$i].Visibility = if (($i + 1) -eq $page) { "Visible" } else { "Collapsed" }
    }
    Update-Stepper $page
    $BtnBack.Visibility = if ($page -in 2, 3) { "Visible" } else { "Collapsed" }
    $BtnCancel.Visibility = if ($page -eq 5) { "Collapsed" } else { "Visible" }
    $BtnNext.Style = $window.FindResource("PrimaryBtn")
    $BtnNext.IsEnabled = $true

    switch ($page) {
        1 { $BtnNext.Content = "Avançar →" }
        2 { $BtnNext.Content = "Avançar →"; Update-PlayniteValidation }
        3 {
            $curPath = $TxtPlaynitePath.Text.Trim()
            if ($script:LastCheckedPlaynitePath -ne $curPath) {
                $script:LastCheckedPlaynitePath = $curPath
                Update-InstalledStatuses $curPath
                Render-SelectionCards
            } else {
                Update-SelectionSummary
            }
        }
        4 {
            $BtnNext.Content = "Instalando..."
            $BtnNext.IsEnabled = $false
            $BtnCancel.IsEnabled = $false
            Start-RealInstallation
        }
        5 {
            $BtnNext.Style = $window.FindResource("SuccessBtn")
            $BtnNext.Content = "Concluir"
            Build-Summary
        }
    }
}

function Build-Summary {
    $TxtSummaryPath.Text = "Pasta de destino: $($TxtPlaynitePath.Text)"
    $SummaryItemsPanel.Children.Clear()

    if (-not $script:InstallSuccess) {
        $FinalBadge.Background = Get-Brush "#2D1E12"
        $FinalBadge.BorderBrush = Get-Brush "#F59E0B"
        $FinalVector.Data = [System.Windows.Media.Geometry]::Parse("M0,0 H3 V9 H0 Z M0,11 H3 V14 H0 Z")
        $FinalVector.Fill = Get-Brush "#F59E0B"
        $TxtFinalTitle.Text = "Falha durante a instalação"
        $TxtFinalSub.Text = $script:InstallErrorMsg
        $TxtFinalSub.Foreground = Get-Brush "#FCD34D"
        $ChkLaunchPlaynite.IsChecked = $false
        $ChkLaunchPlaynite.Visibility = "Collapsed"
    } else {
        $FinalBadge.Background = Get-Brush "#0D2920"
        $FinalBadge.BorderBrush = Get-Brush "#10B981"
        $FinalVector.Data = [System.Windows.Media.Geometry]::Parse("M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z")
        $FinalVector.Fill = Get-Brush "#10B981"
        $TxtFinalTitle.Text = "Instalação Concluída com Sucesso!"
        $TxtFinalSub.Text = "Todos os itens selecionados foram aplicados ao seu Playnite."
        $TxtFinalSub.Foreground = Get-Brush "#6EE7B7"
        $ChkLaunchPlaynite.Visibility = "Visible"

        foreach ($item in ($script:Catalog | Where-Object { $_.Selected })) {
            $row = New-Object System.Windows.Controls.StackPanel
            $row.Orientation = "Horizontal"
            $row.Margin = New-Object System.Windows.Thickness(0,0,0,8)
            $badge = New-VectorIconBadge $item.IconPath $item.Accent $item.IconBg 24
            $badge.Margin = New-Object System.Windows.Thickness(0,0,10,0)
            $txt = New-Object System.Windows.Controls.TextBlock
            $txt.Text = "$($item.Name) ($($item.Version)) - $($item.ActionWord)"
            $txt.FontSize = 12.5; $txt.Foreground = Get-Brush "#A7F3D0"; $txt.VerticalAlignment = "Center"
            $row.Children.Add($badge) | Out-Null
            $row.Children.Add($txt) | Out-Null
            $SummaryItemsPanel.Children.Add($row) | Out-Null
        }
    }
}

# Eventos
$TitleBar.Add_MouseLeftButtonDown({ $window.DragMove() })
$BtnMinimize.Add_Click({ $window.WindowState = "Minimized" })
$BtnClose.Add_Click({ $window.Close() })
$BtnCancel.Add_Click({
    if ($script:WorkerTimer) { $script:WorkerTimer.Stop() }
    $window.Close()
})

$BtnBack.Add_Click({ if ($script:CurrentPage -gt 1) { Go-ToPage ($script:CurrentPage - 1) } })
$BtnNext.Add_Click({
    if ($script:CurrentPage -lt 5) {
        Go-ToPage ($script:CurrentPage + 1)
    } else {
        if ($script:InstallSuccess -and $ChkLaunchPlaynite.IsChecked) {
            $exe = Join-Path $TxtPlaynitePath.Text.Trim() "Playnite.DesktopApp.exe"
            if (Test-Path $exe) { Start-Process $exe }
        }
        $window.Close()
    }
})

$TxtPlaynitePath.Add_TextChanged({ Update-PlayniteValidation })
$BtnAutoDetect.Add_Click({ $TxtPlaynitePath.Text = Find-DefaultPlaynitePath; Update-PlayniteValidation })
$BtnBrowsePath.Add_Click({
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = "Selecione a pasta onde o Playnite está instalado"
    if ($TxtPlaynitePath.Text -and (Test-Path $TxtPlaynitePath.Text)) { $dlg.SelectedPath = $TxtPlaynitePath.Text }
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $TxtPlaynitePath.Text = $dlg.SelectedPath }
})

$TxtSearchFilter.Add_TextChanged({ Render-SelectionCards })
$BtnSelectAll.Add_Click({ foreach ($it in $script:Catalog) { $it.Selected = $true }; Render-SelectionCards })
$BtnUnselectAll.Add_Click({ foreach ($it in $script:Catalog) { $it.Selected = $false }; Render-SelectionCards })

# Inicialização
Load-AppLogo
Render-WelcomeCards
Render-SelectionCards
$TxtPlaynitePath.Text = if ($PlaynitePath) { $PlaynitePath } else { Find-DefaultPlaynitePath }
Go-ToPage 1
$window.ShowDialog() | Out-Null
