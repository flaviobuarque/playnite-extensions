# 🎮 Playnite Extensions & Themes Suite

<p align="center">
  <img src="assets/playnite-logo.png" width="96" alt="Playnite Logo" />
</p>

<p align="center">
  <strong>Monorepo oficial de extensões de alta performance e temas customizados para o Playnite 10+</strong>
</p>

<p align="center">
  <a href="https://playnite.link"><img src="https://img.shields.io/badge/Playnite-v10.x-FF5E3A?style=for-the-badge&logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0id2hpdGUiPjxwYXRoIGQ9Ik03LjUgNkM0LjUgNiAyIDguOCAyIDEyLjhjMCAyLjcgMS4xIDUuMiAyLjggNS4yIDEuMiAwIDItMS4yIDMuMS0yLjUuNi0uNyAxLjMtMSAyLjEtMWg0Yy44IDAgMS41LjMgMi4xIDEgMS4xIDEuMyAxLjkgMi41IDMuMSAyLjUgMS43IDAgMi44LTIuNSAyLjgtNS4yQzIyIDguOCAxOS41IDYgMTYuNSA2Yy0xLjUgMC0yLjUuNy00LjUuN1M5IDYgNy41IDZ6bS41IDQuMmExLjggMS44IDAgMSAxIDAgMy42IDEuOCAxLjggMCAwIDEgMC0zLjZ6bTctMWExIDEgMCAxIDEgMCAyIDEgMSAwIDAgMSAwLTJ6bS0yIDJhMSAxIDAgMSAxIDAgMiAxIDEgMCAwIDEgMC0yem00IDBhMSAxIDAgMSAxIDAgMiAxIDEgMCAwIDEgMC0yem0tMiAyYTEgMSAwIDEgMSAwIDIgMSAxIDAgMCAxIDAtMnoiLz48L3N2Zz4=" alt="Playnite v10" /></a>
  <a href="https://github.com/flaviobuarque/playnite-extensions/actions"><img src="https://img.shields.io/github/actions/workflow/status/flaviobuarque/playnite-extensions/release.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=CI%2FCD" alt="CI/CD" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET%20Framework-4.6.2-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 4.6.2" /></a>
  <a href="https://learn.microsoft.com/en-us/dotnet/csharp/"><img src="https://img.shields.io/badge/Language-C%23%2010-239120?style=for-the-badge&logo=dotnet&logoColor=white" alt="C# 10" /></a>
  <a href="#-licença"><img src="https://img.shields.io/badge/License-MIT-success?style=for-the-badge" alt="MIT License" /></a>
</p>

---

## ⚡ Instalação Rápida em 1 Linha (Assistente Visual WPF)

Instale qualquer extensão ou tema diretamente no seu Playnite usando o assistente gráfico, sem precisar clonar ou compilar o repositório. Abra o **PowerShell** e execute:

```powershell
irm https://raw.githubusercontent.com/flaviobuarque/playnite-extensions/main/install.ps1 | iex
```

> **O que o assistente gráfico faz em 5 etapas:**
> 1. **Início:** Carrega dinamicamente o catálogo (`catalog.json`) com todos os plugins e temas disponíveis.
> 2. **Playnite:** Localiza automaticamente a instalação (ou permite selecionar manualmente uma pasta Portable).
> 3. **Pacotes:** Exibe checkboxes com busca rápida para você marcar apenas o que deseja instalar.
> 4. **Instalação:** Fecha o Playnite com segurança (com confirmação), baixa os pacotes da última release e aplica nos diretórios corretos.
> 5. **Concluir:** Mostra o resumo da instalação e oferece a opção de abrir o Playnite imediatamente.

*(Modo terminal avançado sem interface: `.\install.ps1 -Silent -ForceClose`)*

---

## 📦 Componentes do Repositório

| Componente | Tipo | Descrição | Documentação |
|---|---|---|---|
| **External Game Tracker** | Extensão C# | Detecta jogos abertos externamente (Battle.net, Steam, atalhos) via WMI Kernel Trace sem consumo contínuo de CPU, com cache $O(1)$ e compatibilidade com anti-cheats (Ricochet, EAC, BattlEye). | [Ver Documentação](extensions/ExternalGameTracker/README.md) |
| **Installation Status Updater** | Extensão C# | Detecção rápida de jogos instalados em múltiplos discos com controle de concorrência I/O, regras de tamanho mínimo e scanner dedicado de pastas órfãs. | [Ver Documentação](extensions/InstallationStatusUpdater/README.md) |
| **Helium (Running Highlights)** | Tema Desktop | Tema moderno customizado com badge minimalista `● RODANDO`, botão Play que alterna para Stop ⏹, borda neon esmeralda e barra de título destacada. | [Ver Documentação](themes/Helium/README.md) |

---

## 🏛️ Estrutura do Monorepo

```text
playnite-extensions/
├── .github/
│   └── workflows/
│       └── release.yml                 # Pipeline CI/CD (compila tudo e gera release por tag)
├── assets/
│   └── playnite-logo.png               # Logo oficial usada nos READMEs e instalador
├── extensions/                         # Coleção de extensões Playnite (.NET 4.6.2 / SDK-style)
│   ├── ExternalGameTracker/            # Rastreamento externo via WMI Kernel Trace
│   │   ├── README.md
│   │   └── ...
│   └── InstallationStatusUpdater/      # Scanner assíncrono de instalação e pastas órfãs
│       ├── README.md
│       └── ...
├── themes/                             # Coleção de temas customizados
│   └── Helium/                         # Backup completo do tema Helium modificado
│       ├── README.md
│       ├── DerivedStyles/              # GridViewItemStyle e GridViewItemTemplate modificados
│       └── ...
├── releases/                           # Pacotes compilados localmente (.pext e .zip)
├── catalog.json                        # Catálogo escalável consumido pela GUI do instalador
├── install.ps1                         # Instalador gráfico WPF Wizard (Web irm|iex e local)
└── README.md                           # Este documento
```

---

## 🛠️ Como Compilar Manualmente (Desenvolvedores)

### Pré-requisitos
- [.NET SDK 6.0+](https://dotnet.microsoft.com/download) instalado.
- [Playnite 10+](https://playnite.link) instalado.
- Git.

### 1. Clonar e Compilar as Extensões
```powershell
git clone https://github.com/flaviobuarque/playnite-extensions.git
cd playnite-extensions

dotnet build extensions/ExternalGameTracker/ExternalGameTracker.csproj -c Release
dotnet build extensions/InstallationStatusUpdater/InstallationStatusUpdater.csproj -c Release
```

### 2. Empacotar os `.pext` (Playnite Toolbox)
```powershell
& "$env:LOCALAPPDATA\Playnite\Toolbox.exe" pack "extensions\ExternalGameTracker\bin\Release" "releases"
& "$env:LOCALAPPDATA\Playnite\Toolbox.exe" pack "extensions\InstallationStatusUpdater\bin\Release" "releases"
```

### 3. Executar o Instalador Local
```powershell
.\install.ps1
```

---

## 🔄 Pipeline de CI/CD (GitHub Actions)

O repositório conta com uma esteira automatizada em [`.github/workflows/release.yml`](.github/workflows/release.yml):
- **Integração Contínua (CI):** A cada push na branch `main`, todas as extensões são compiladas em `Release` para validação, **sem criar release**.
- **Entrega Contínua (CD / Releases por Tag):** As releases são publicadas apenas ao subir uma tag. O workflow empacota **todos os componentes** nos *Assets* e gera automaticamente a tabela destacando apenas o que mudou:
  - `egt-v1.0.1` → Destaca apenas o **External Game Tracker** como atualizado.
  - `isu-v2.5.5` → Destaca apenas o **Installation Status Updater** como atualizado.
  - `helium-v1.1.0` → Destaca apenas o **Tema Helium** como atualizado.
  - `v1.1.0` → Tag global (detecta automaticamente pelo `git diff` quais pastas mudaram).
  ```powershell
  git tag -a v1.1.0 -m "- Descrição das novidades"
  git push origin v1.1.0
  ```

---

## 📄 Licença

Distribuído sob a licença **MIT**. Consulte o arquivo `LICENSE` para obter mais informações.
