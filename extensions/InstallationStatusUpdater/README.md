# 📦 Installation Status Updater (Playnite Extension)

<p align="center">
  <img src="../../assets/playnite-logo.png" width="80" alt="Playnite Logo" />
</p>

<p align="center">
  <strong>Extensão avançada para detecção rápida de jogos instalados nos discos, controle de I/O e limpeza de pastas órfãs.</strong>
</p>

<p align="center">
  <a href="https://playnite.link"><img src="https://img.shields.io/badge/Playnite-v10.x-FF5E3A?style=for-the-badge&logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0id2hpdGUiPjxwYXRoIGQ9Ik03LjUgNkM0LjUgNiAyIDguOCAyIDEyLjhjMCAyLjcgMS4xIDUuMiAyLjggNS4yIDEuMiAwIDItMS4yIDMuMS0yLjUuNi0uNyAxLjMtMSAyLjEtMWg0Yy44IDAgMS41LjMgMi4xIDEgMS4xIDEuMyAxLjkgMi41IDMuMSAyLjUgMS43IDAgMi44LTIuNSAyLjgtNS4yQzIyIDguOCAxOS41IDYgMTYuNSA2Yy0xLjUgMC0yLjUuNy00LjUuN1M5IDYgNy41IDZ6bS41IDQuMmExLjggMS44IDAgMSAxIDAgMy42IDEuOCAxLjggMCAwIDEgMC0zLjZ6bTctMWExIDEgMCAxIDEgMCAyIDEgMSAwIDAgMSAwLTJ6bS0yIDJhMSAxIDAgMSAxIDAgMiAxIDEgMCAwIDEgMC0yem00IDBhMSAxIDAgMSAxIDAgMiAxIDEgMCAwIDEgMC0yem0tMiAyYTEgMSAwIDEgMSAwIDIgMSAxIDAgMCAxIDAtMnoiLz48L3N2Zz4=" alt="Playnite v10" /></a>
  <img src="https://img.shields.io/badge/I%2FO-Disk%20Concurrency%20Limiter-38BDF8?style=for-the-badge" alt="Disk Concurrency" />
  <img src="https://img.shields.io/badge/Cleanup-Orphan%20Folder%20Scanner-brightgreen?style=for-the-badge" alt="Orphan Folder Scanner" />
  <img src="https://img.shields.io/badge/.NET%20Framework-4.6.2-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 4.6.2" />
</p>

---

## 🎯 Por Que Esta Extensão Existe?

Bibliotecas modernas com centenas de jogos espalhados por múltiplos SSDs e HDDs costumam enfrentar lentidão ou falsos positivos ao verificar o status de instalação.

Esta versão customizada (**Local v2.54**) do **Installation Status Updater** oferece:

- ⚡ **Controle de Concorrência por Disco (`DiskConcurrencyLimiter`):** Semáforo inteligente por unidade física que evita gargalos de leitura em HDs mecânicos.
- 💾 **Cache de Diretórios e Tamanhos (`DirectorySizeCache`):** Evita recalcular gigabytes repetidamente sem necessidade.
- 📏 **Regras de Tamanho Mínimo (`MinimumInstallationSizeRule`):** Impede que pastas residuais de save ou config (ex.: 50 KB) marquem um jogo desinstalado como instalado.
- 🔌 **Detecção de HDs Externos (`DeviceEventWatcherService`):** Atualiza automaticamente sua biblioteca ao conectar ou desconectar drives removíveis.
- 🧹 **Scanner de Pastas Órfãs (`OrphanFoldersScanner`):** Identifica pastas antigas que sobraram nos diretórios monitorados e não pertencem mais a nenhum jogo da biblioteca, permitindo inspeção e limpeza rápida com interface dedicada.
- 🏷️ **Filtros e Badges de Origem (`SourceFilterItem` / `SourceBadgeProvider`):** Janela de resultados aprimorada com filtragem por fonte e visualização clara do motivo da mudança de status.

---

## 🏛️ Arquitetura dos Módulos

```text
InstallationStatusUpdater/
├── Application/
│   ├── DeviceEventWatcherService.cs       # Eventos de conexão/desconexão de drives (Win32)
│   ├── DirectorySizeCache.cs              # Cache em memória de tamanhos calculados
│   ├── DirectoryWatcherService.cs         # FileSystemWatcher para pastas monitoradas
│   ├── DiskConcurrencyLimiter.cs          # Controle de concorrência por letra de disco
│   ├── GameFolderRootResolver.cs          # Resolução precisa da raiz de instalação
│   ├── GamesInstallationStatusScanner.cs  # Motor central assíncrono de varredura
│   ├── MinimumInstallationSizeRule.cs     # Validação de tamanho mínimo por plataforma/jogo
│   ├── OrphanFoldersScanner.cs            # Scanner de diretórios órfãos sem vínculo
│   └── TagsUpdater.cs                     # Atualização automática de tags na biblioteca
├── Domain/                                # Entidades, resultados e objetos de valor
├── Presentation/                          # Janelas WPF (Resultados, Pastas Órfãs, Configs)
├── Localization/                          # Arquivos de tradução (inclui pt_BR e en_US)
├── extension.yaml                         # Manifesto da extensão (Id: Installation_Status_Updater_Local)
└── InstallationStatusUpdater.cs           # Classe principal (GenericPlugin)
```

---

## 💻 Compilação Manual

O projeto já está convertido para o formato moderno **SDK-style** (`.NET Framework 4.6.2` + `UseWPF`), compilando diretamente via `dotnet`:

```powershell
cd extensions/InstallationStatusUpdater
dotnet restore
dotnet build -c Release
```

Para empacotar no formato `.pext` oficial do Playnite:

```powershell
& "$env:LOCALAPPDATA\Playnite\Toolbox.exe" pack "bin\Release" "..\..\releases"
```

---

## ⚡ Instalação Rápida

Para instalar com interface guiada direto da última Release oficial:

```powershell
irm https://raw.githubusercontent.com/flaviobuarque/playnite-extensions/main/install.ps1 | iex
```
