<#
.SYNOPSIS
    Instalador Universal do Playnite Extensions & Themes.
.DESCRIPTION
    Script de instalação automatizada para extensões e temas customizados do Playnite.
    Pode ser executado localmente ou diretamente via Web:
    irm https://raw.githubusercontent.com/flaviobuarque/playnite-extensions/main/install.ps1 | iex
#>

param(
    [Parameter(Position=0)]
    [ValidateSet("All", "ExternalGameTracker", "HeliumTheme", "Interactive")]
    [string]$Target = "Interactive",

    [switch]$NoPrompt,
    [switch]$ForceClose
)

$ErrorActionPreference = "Stop"

# URLs oficiais de releases
$RepoOwner = "flaviobuarque"
$RepoName = "playnite-extensions"
$LatestReleaseApi = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
$script:CachedRelease = $null

function Get-LatestReleaseInfo {
    if (-not $script:CachedRelease) {
        Write-Host "Consultando última release no GitHub..." -ForegroundColor DarkGray
        $script:CachedRelease = Invoke-RestMethod -Uri $LatestReleaseApi -UseBasicParsing
    }
    return $script:CachedRelease
}

function Write-Banner {
    Clear-Host
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host "     🎮 PLAYNITE EXTENSIONS & THEMES - UNIVERSAL INSTALLER 🎮    " -ForegroundColor Green
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Find-PlaynitePath {
    $defaultPath = "$env:LOCALAPPDATA\Playnite"
    if (Test-Path "$defaultPath\Playnite.DesktopApp.exe") {
        return $defaultPath
    }

    # Busca no registro caso instalado em diretório customizado
    $regPaths = @(
        "HKCU:\Software\Playnite",
        "HKLM:\SOFTWARE\Playnite",
        "HKLM:\SOFTWARE\WOW6432Node\Playnite"
    )
    foreach ($reg in $regPaths) {
        if (Test-Path $reg) {
            $val = (Get-ItemProperty -Path $reg -ErrorAction SilentlyContinue).InstallDir
            if ($val -and (Test-Path "$val\Playnite.DesktopApp.exe")) {
                return $val
            }
        }
    }

    return $null
}

function Close-PlayniteIfRunning([string]$playnitePath) {
    $processes = Get-Process -Name "*Playnite*" -ErrorAction SilentlyContinue
    if ($processes) {
        Write-Host "⚠️  Detectamos que o Playnite está em execução." -ForegroundColor Yellow
        if (-not $ForceClose) {
            $resp = Read-Host "Deseja encerrar o Playnite para evitar bloqueio de arquivos? (S/N)"
            if ($resp -notmatch "^[sSyY]") {
                Write-Host "❌ Instalação cancelada pelo usuário." -ForegroundColor Red
                exit 1
            }
        }
        Write-Host "Fechando processos do Playnite..." -ForegroundColor Cyan
        $processes | Stop-Process -Force
        Start-Sleep -Seconds 1
    }
}

function Install-ExternalGameTracker([string]$playnitePath, [string]$tempDir) {
    Write-Host "`n📦 [1/2] Instalando extensão: External Game Tracker..." -ForegroundColor Cyan
    
    $extPluginId = "ExternalGameTracker_6f982b13-054d-44a6-b51c-81b4d0eb38c9"
    $targetDir = "$playnitePath\Extensions\$extPluginId"
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    # Verifica se existe pacote local (quando executado do repositório) ou baixa da última release
    $localPext = if ($PSScriptRoot) { Get-ChildItem -Path "$PSScriptRoot\releases" -Filter "ExternalGameTracker*.pext" -ErrorAction SilentlyContinue | Select-Object -First 1 } else { $null }
    $downloadFile = "$tempDir\ExternalGameTracker.pext"

    if ($localPext) {
        Write-Host "Usando arquivo compilado local: $($localPext.Name)" -ForegroundColor DarkGray
        Copy-Item -Path $localPext.FullName -Destination $downloadFile -Force
    } else {
        $release = Get-LatestReleaseInfo
        $asset = $release.assets | Where-Object { $_.name -like "ExternalGameTracker*.pext" } | Select-Object -First 1
        if (-not $asset) {
            throw "Nenhum pacote .pext encontrado na última release ($($release.tag_name))."
        }
        Write-Host "Baixando release $($release.tag_name): $($asset.browser_download_url)" -ForegroundColor DarkGray
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $downloadFile -UseBasicParsing
    }

    # Descompacta o .pext (zip) no diretório de extensões
    Write-Host "Extraindo arquivos da extensão em: $targetDir" -ForegroundColor DarkGray
    Expand-Archive -Path $downloadFile -DestinationPath $targetDir -Force

    Write-Host "✅ Extensão External Game Tracker instalada com sucesso!" -ForegroundColor Green
}

function Install-HeliumTheme([string]$playnitePath, [string]$tempDir) {
    Write-Host "`n🎨 [2/2] Instalando tema: Helium Customizado (Running Highlights)..." -ForegroundColor Cyan
    
    $themeId = "8b15c46a-90c2-4fe5-9ebb-1ab25ba7fcb1"
    $targetDir = "$playnitePath\Themes\Desktop\$themeId"
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    # Se estiver executando localmente e tiver a pasta themes/Helium
    $localThemeDir = if ($PSScriptRoot) { "$PSScriptRoot\themes\Helium" } else { $null }
    $localThemeZip = if ($PSScriptRoot) { "$PSScriptRoot\releases\Helium-Custom-Theme.zip" } else { $null }
    $downloadFile = "$tempDir\Helium-Custom-Theme.zip"

    if ($localThemeDir -and (Test-Path $localThemeDir)) {
        Write-Host "Copiando tema da pasta local..." -ForegroundColor DarkGray
        Copy-Item -Path "$localThemeDir\*" -Destination $targetDir -Recurse -Force
    } elseif ($localThemeZip -and (Test-Path $localThemeZip)) {
        Write-Host "Usando arquivo zip local..." -ForegroundColor DarkGray
        Expand-Archive -Path $localThemeZip -DestinationPath $targetDir -Force
    } else {
        $release = Get-LatestReleaseInfo
        $asset = $release.assets | Where-Object { $_.name -like "Helium*.zip" } | Select-Object -First 1
        if (-not $asset) {
            throw "Nenhum arquivo zip do tema encontrado na última release ($($release.tag_name))."
        }
        Write-Host "Baixando release $($release.tag_name): $($asset.browser_download_url)" -ForegroundColor DarkGray
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $downloadFile -UseBasicParsing
        Expand-Archive -Path $downloadFile -DestinationPath $targetDir -Force
    }

    Write-Host "✅ Tema Helium Customizado instalado com sucesso!" -ForegroundColor Green
}

# --- Início do Script ---
Write-Banner

# 1. Localiza o Playnite
$playnitePath = Find-PlaynitePath
if (-not $playnitePath) {
    Write-Host "❌ Erro: O Playnite não foi localizado nesta máquina." -ForegroundColor Red
    Write-Host "Verifique se o Playnite está instalado em %LOCALAPPDATA%\Playnite." -ForegroundColor Yellow
    exit 1
}
Write-Host "Localização do Playnite detectada: $playnitePath" -ForegroundColor DarkGray

# 2. Menu Interativo (caso Target seja Interactive)
if ($Target -eq "Interactive" -and -not $NoPrompt) {
    Write-Host "`nSelecione o que deseja instalar:" -ForegroundColor Yellow
    Write-Host " [1] Instalar TUDO (Extensões + Tema Helium Customizado)" -ForegroundColor White
    Write-Host " [2] Instalar apenas a Extensão ExternalGameTracker" -ForegroundColor White
    Write-Host " [3] Instalar apenas o Tema Helium Customizado" -ForegroundColor White
    Write-Host " [0] Cancelar" -ForegroundColor DarkGray
    Write-Host ""
    $choice = Read-Host "Digite a opção desejada [1-3]"
    switch ($choice) {
        "1" { $Target = "All" }
        "2" { $Target = "ExternalGameTracker" }
        "3" { $Target = "HeliumTheme" }
        default {
            Write-Host "Operação cancelada." -ForegroundColor Yellow
            exit 0
        }
    }
}

# 3. Fecha o Playnite se estiver aberto
Close-PlayniteIfRunning -playnitePath $playnitePath

# 4. Cria pasta temporária
$tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "PlayniteInstaller_" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    if ($Target -eq "All" -or $Target -eq "ExternalGameTracker") {
        Install-ExternalGameTracker -playnitePath $playnitePath -tempDir $tempDir
    }
    if ($Target -eq "All" -or $Target -eq "HeliumTheme") {
        Install-HeliumTheme -playnitePath $playnitePath -tempDir $tempDir
    }

    Write-Host "`n🎉 ========================================================" -ForegroundColor Green
    Write-Host "   Instalação concluída com sucesso no seu Playnite!     " -ForegroundColor Green
    Write-Host "========================================================`n" -ForegroundColor Green

    if (-not $NoPrompt) {
        $reopen = Read-Host "Deseja abrir o Playnite agora? (S/N)"
        if ($reopen -match "^[sSyY]") {
            Start-Process "$playnitePath\Playnite.DesktopApp.exe"
            Write-Host "Playnite iniciado." -ForegroundColor Cyan
        }
    }
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
