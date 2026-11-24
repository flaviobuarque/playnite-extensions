# 🎨 Helium Desktop Theme (Active Running Highlights Mod)

<p align="center">
  <img src="../../assets/playnite-logo.png" width="80" alt="Playnite Logo" />
</p>

<p align="center">
  <strong>Modificação visual moderna e minimalista para o tema Helium no Playnite 10+, destacando jogos em execução.</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Playnite%20Theme%20API-2.8.0-FF5E3A?style=for-the-badge&logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCIgZmlsbD0id2hpdGUiPjxwYXRoIGQ9Ik03LjUgNkM0LjUgNiAyIDguOCAyIDEyLjhjMCAyLjcgMS4xIDUuMiAyLjggNS4yIDEuMiAwIDItMS4yIDMuMS0yLjUuNi0uNyAxLjMtMSAyLjEtMWg0Yy44IDAgMS41LjMgMi4xIDEgMS4xIDEuMyAxLjkgMi41IDMuMSAyLjUgMS43IDAgMi44LTIuNSAyLjgtNS4yQzIyIDguOCAxOS41IDYgMTYuNSA2Yy0xLjUgMC0yLjUuNy00LjUuN1M5IDYgNy41IDZ6bS41IDQuMmExLjggMS44IDAgMSAxIDAgMy42IDEuOCAxLjggMCAwIDEgMC0zLjZ6bTctMWExIDEgMCAxIDEgMCAyIDEgMSAwIDAgMSAwLTJ6bS0yIDJhMSAxIDAgMSAxIDAgMiAxIDEgMCAwIDEgMC0yem00IDBhMSAxIDAgMSAxIDAgMiAxIDEgMCAwIDEgMC0yem0tMiAyYTEgMSAwIDEgMSAwIDIgMSAxIDAgMCAxIDAtMnoiLz48L3N2Zz4=" alt="Theme API 2.8.0" />
  <img src="https://img.shields.io/badge/Theme%20Base-Helium%20v1.71-blue?style=for-the-badge" alt="Helium v1.71" />
  <img src="https://img.shields.io/badge/UI%20Framework-WPF%20%2F%20XAML-purple?style=for-the-badge" alt="WPF / XAML" />
  <img src="https://img.shields.io/badge/Trigger-IsRunning%20%3D%20True-brightgreen?style=for-the-badge" alt="IsRunning Trigger" />
</p>

---

## ✨ Recursos Visuais Implementados

Esta modificação do tema **Helium** reage dinamicamente quando qualquer jogo entra no estado `IsRunning = True` (seja aberto pelo Playnite ou detectado em segundo plano pela extensão [External Game Tracker](../../extensions/ExternalGameTracker/README.md)):

```text
┌──────────────────────────────────────────────┐
│  (Bnet) BATTLE.NET                        ★  │
│  [ ● RODANDO ]                               │ <── 1. Badge minimalista no padrão nativo
├──────────────────────────────────────────────┤
│                                              │
│                                              │
│                  [ CAPA ]                    │
│                                              │
│                                              │
│                                              │
│ [ ⏹ ]                                    (i) │ <── 2. Botão Play vira Stop vermelho suave
├──────────────────────────────────────────────┤
│  Call of Duty: Warzone                       │ <── 3. Barra inferior em degradê esmeralda
└──────────────────────────────────────────────┘
  ▲
  └── 4. Borda do card inteiro em Verde Neon (#00FF88) com DropShadow Glow de 12px
```

### 1. Tag de Status Minimalista `[ ● RODANDO ]`
- **Design Padronizado:** Construída com a mesma linguagem visual das tags nativas de plataforma do Helium (como a tag `[ PS5 ]` e `[ PC PORT ]`).
- **Dimensões e Cores:**
  - `CornerRadius="3"`.
  - Fundo escuro translúcido (`#D9101622`).
  - Borda fina esmeralda de 1px (`#10B981`).
  - Ponto luminoso verde circular de 5px.
  - Tipografia limpa de 9px em negrito (`#E6F4EA`).
- **Posicionamento:** Alinhado no topo esquerdo, posicionado logo abaixo do banner da plataforma para não obstruir logotipos nem artes principais da capa.

### 2. Alternância do Botão Play ▶ para Stop ⏹
- O botão inferior esquerdo (`PART_ButtonPlay`) agora conta com vetor de geometria para o ícone de parada.
- Quando o jogo está em execução:
  - O triângulo de Play (`▶`) é ocultado.
  - O quadrado de Stop com cantos arredondados (`⏹`) em tom vermelho suave (`#FF5252`) torna-se visível.
  - A barra inferior de controles permanece visível durante toda a sessão de jogo.

### 3. Borda Externa com Brilho Neon (`#00FF88`)
- A borda de seleção externa acende em tom verde esmeralda neon (`#00FF88`).
- Conta com efeito difuso de sombra suave (`DropShadowEffect` com raio de desfoque de 12px).
- Permite identificar instantaneamente qual jogo está aberto, mesmo com o Playnite em uma tela secundária ou sem foco.

### 4. Destaque da Barra de Título
- O rodapé com o nome do jogo ganha um preenchimento em gradiente esmeralda escuro (`#003D1D` a `#007A3D`).
- A tipografia do título do jogo passa a ser exibida em negrito e na cor verde neon (`#00FF88`).

---

## 📂 Arquivos Modificados (XAML)

As modificações estão concentradas na pasta `DerivedStyles/`:

1. **[`DerivedStyles/GridViewItemTemplate.xaml`](DerivedStyles/GridViewItemTemplate.xaml):**
   - Inclusão do elemento `ActiveRunningBadge`.
   - Implementação do vetor `StopIcon` dentro do estilo `ButtonPlay`.
   - Triggers `DataTrigger Binding="{Binding IsRunning}" Value="True"` para alternar o botão para `"Running"`, exibir o badge e aplicar o degradê no título.
2. **[`DerivedStyles/GridViewItemStyle.xaml`](DerivedStyles/GridViewItemStyle.xaml):**
   - Configuração do `SelectionBorder` para disparar a borda `#00FF88` e o efeito de brilho `DropShadowEffect BlurRadius="12"` sob a condição `IsRunning == True`.

---

## 🚀 Instalação Manual do Tema

Se preferir instalar manualmente sem o script automático:

1. Feche o Playnite.
2. Copie todo o conteúdo desta pasta (`themes/Helium/*`) para:
   ```text
   %LOCALAPPDATA%\Playnite\Themes\Desktop\8b15c46a-90c2-4fe5-9ebb-1ab25ba7fcb1
   ```
3. Abra o Playnite. O tema atualizado será carregado na memória.
