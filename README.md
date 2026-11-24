# 🎮 Playnite Extensions & Themes Suite

<p align="center">
  <img src="https://raw.githubusercontent.com/JosefNemec/Playnite/master/source/Playnite/Resources/Icons/applogo.png" width="96" alt="Playnite Logo" />
</p>

<p align="center">
  <strong>Monorepo oficial de extensões de alta performance e temas customizados para o Playnite 10+</strong>
</p>

<p align="center">
  <a href="https://playnite.link"><img src="https://img.shields.io/badge/Playnite-v10.x-orange?style=for-the-badge&logo=playnite&logoColor=white" alt="Playnite v10" /></a>
  <a href="https://github.com/flaviobuarque/playnite-extensions/actions"><img src="https://img.shields.io/badge/CI%2FCD-GitHub%20Actions-brightgreen?style=for-the-badge&logo=githubactions&logoColor=white" alt="CI/CD" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET%20Framework-4.6.2-purple?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 4.6.2" /></a>
  <a href="https://learn.microsoft.com/en-us/dotnet/csharp/"><img src="https://img.shields.io/badge/Language-C%23%2010-blue?style=for-the-badge&logo=csharp&logoColor=white" alt="C# 10" /></a>
  <a href="#licença"><img src="https://img.shields.io/badge/License-MIT-success?style=for-the-badge" alt="MIT License" /></a>
</p>

---

## ⚡ Instalação Rápida em 1 Linha (PowerShell)

Instale qualquer extensão ou tema diretamente no seu Playnite sem precisar clonar ou compilar o repositório. Abra o **PowerShell** e execute:

```powershell
irm https://raw.githubusercontent.com/flaviobuarque/playnite-extensions/main/install.ps1 | iex
```

> **O que o instalador faz automaticamente:**
> 1. Localiza a instalação do Playnite na sua máquina (`%LOCALAPPDATA%\Playnite` ou Registro).
> 2. Fecha o Playnite se estiver em execução para evitar bloqueios de arquivos DLL/XAML.
> 3. Apresenta um menu seletivo para você escolher o que deseja instalar:
>    - `[1]` Instalar Tudo (Extensões + Tema)
>    - `[2]` Instalar apenas a Extensão ExternalGameTracker
>    - `[3]` Instalar apenas o Tema Helium Customizado
> 4. Baixa os arquivos compilados da release mais recente e realiza o deploy nas pastas corretas.
> 5. Oferece a opção de abrir o Playnite pronto para jogar.

---

## 📦 Componentes do Repositório

| Componente | Tipo | Descrição | Documentação |
|---|---|---|---|
| **External Game Tracker** | Extensão C# | Detecta jogos abertos externamente (Battle.net, Steam, atalhos) via WMI Kernel Trace sem consumo contínuo de CPU, com cache $O(1)$ e compatibilidade com anti-cheats (Ricochet, EAC, BattlEye). | [Ver Documentação](extensions/ExternalGameTracker/README.md) |
| **Helium (Running Highlights)** | Tema Desktop | Customização completa do tema Helium com badge minimalista `● RODANDO`, botão de Play que vira Stop ⏹ (`#FF5252`), borda neon esmeralda (`#00FF88`) e barra de título destacada. | [Ver Documentação](themes/Helium/README.md) |

---

## 🏛️ Estrutura do Monorepo

```text
playnite-extensions/
├── .github/
│   └── workflows/
│       └── release.yml                 # Pipeline CI/CD que compila e gera releases na nuvem
├── extensions/                         # Coleção de extensões Playnite
│   └── ExternalGameTracker/            # Extensão de rastreamento de processos externos
│       ├── README.md                   # Documentação detalhada da extensão
│       ├── ExternalGameTracker.csproj  # Projeto .NET 4.6.2 / C# 10
│       ├── ExternalGameTrackerPlugin.cs# Classe principal do plugin
│       ├── extension.yaml              # Manifesto oficial da extensão
│       ├── Models/                     # P/Invoke QueryFullProcessImageName com retry
│       ├── Services/                   # LibraryIndexer O(1), ProcessWatcher WMI, SessionManager
│       ├── Settings/                   # Configurações e ViewModel
│       └── Views/                      # Interface de configurações XAML
├── themes/                             # Coleção de temas customizados
│   └── Helium/                         # Backup completo do tema Helium modificado
│       ├── README.md                   # Guia visual detalhado do tema
│       ├── theme.yaml                  # Manifesto oficial do tema
│       ├── Constants.xaml              # Variáveis e estilos
│       ├── DerivedStyles/              # GridViewItemStyle e GridViewItemTemplate modificados
│       └── ...                         # Controles, imagens e views
├── releases/                           # Pacotes compilados de distribuição (.pext e .zip)
├── install.ps1                         # Script instalador universal (Web irm|iex e local)
└── README.md                           # Este documento
```

---

## 🛠️ Como Compilar Manualmente (Desenvolvedores)

Se você desejar clonar o repositório e compilar por conta própria:

### Pré-requisitos
- [.NET SDK 6.0+](https://dotnet.microsoft.com/download) instalado.
- [Playnite 10+](https://playnite.link) instalado.
- Git.

### 1. Clonar o Repositório
```powershell
git clone https://github.com/flaviobuarque/playnite-extensions.git
cd playnite-extensions
```

### 2. Compilar a Extensão
```powershell
cd extensions/ExternalGameTracker
dotnet restore
dotnet build -c Release
```
Os binários compilados estarão localizados em `extensions/ExternalGameTracker/bin/Release/`.

### 3. Empacotar o `.pext` (Playnite Toolbox)
```powershell
& "$env:LOCALAPPDATA\Playnite\Toolbox.exe" pack "bin\Release" "..\..\releases"
```

### 4. Executar o Instalador Local
Você pode rodar o instalador a partir da raiz do repositório local:
```powershell
cd ..\..
.\install.ps1
```

---

## 🔄 Pipeline de CI/CD (GitHub Actions)

O repositório conta com uma esteira automatizada em [`.github/workflows/release.yml`](.github/workflows/release.yml):
- **Gatilho:** A cada push na branch `main` ou publicação de tag.
- **Ambiente:** Máquina virtual `windows-latest`.
- **Etapas:** Restaura dependências, compila a extensão em `Release`, obtém o `Toolbox.exe` oficial do Playnite, empacota o `.pext`, compacta o tema Helium em `.zip` e cria um **GitHub Release** público com os artefatos prontos.

---

## 📄 Licença

Distribuído sob a licença **MIT**. Consulte o arquivo `LICENSE` para obter mais informações.
