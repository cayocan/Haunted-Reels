# 🎃 Haunted Reels

> Slot machine Halloween desenvolvida como **teste técnico para a Hourglass Studios**.
> Grid 5×3 · 10 Paylines · Spine Animations · Build WebGL · Backend Provably Fair

---

## 🕹️ Demo & Links

| | |
|---|---|
| **▶ Jogar (GitHub Pages)** | https://cayocan.github.io/Haunted-Reels |
| **📁 Repositório** | https://github.com/cayocan/Haunted-Reels |

---

## ✅ Requisitos do Teste — Implementados

### Grid e Motor
| Spec | Implementado |
|------|-------------|
| Grid 5 reels × 3 linhas | ✅ |
| 10 paylines fixas, sempre ativas | ✅ |
| Direção: esquerda → direita a partir do reel 1 | ✅ |
| Mínimo 3 símbolos consecutivos idênticos | ✅ |
| Cálculo: multiplicador × (aposta ÷ 10) | ✅ |
| Aposta base 1,00 crédito | ✅ com botão de alteração de aposta |

### Símbolos e Paytable

| Código | Símbolo | 3× | 4× | 5× |
|--------|---------|----|----|-----|
| H1 | 🧙 Bruxa (Spine skin: `witch`) | 5× | 25× | 150× |
| H2 | 🎃 Abóbora (Spine skin: `pumpkin`) | 4× | 15× | 100× |
| H3 | 💀 Caveira (Spine skin: `skull`) | 3× | 10× | 60× |
| L1 | 🦇 Morcego | 2× | 8× | 30× |
| L2 | 🕷 Aranha | 1,5× | 6× | 20× |
| L3 | 🧪 Poção | 1× | 4× | 15× |
| W | 👻 Wild Fantasma | substitui qualquer símbolo exceto Scatter | — | — |

### Integração Spine
- `HalloweenCreature.skel` com skins `witch`, `pumpkin`, `skull` aplicadas via `SkeletonGraphic`
- Animação **idle** em loop nos reels parados
- Animação **die** tocada ao confirmar vitória do símbolo
- Fallback DOTween (scale + shake) para símbolos sem Spine (L1–L3 e Wild)

### Paylines implementadas
```
#1  Linha do meio:   [1,1,1,1,1]
#2  Linha de cima:   [0,0,0,0,0]
#3  Linha de baixo:  [2,2,2,2,2]
#4  V invertido:     [0,1,2,1,0]
#5  V normal:        [2,1,0,1,2]
#6  Diagonal ↘:     [0,0,1,2,2]
#7  Diagonal ↗:     [2,2,1,0,0]
#8  Escada ↘:       [0,1,1,2,2]
#9  Escada ↗:       [2,1,1,0,0]
#10 Onda suave ↘:   [0,1,1,1,2]
```

---

## 🚀 Extras Implementados

### 🔐 Backend Provably Fair (Node.js + Fastify)
O jogo possui um backend REST completo — não é um slot client-only. Cada resultado é criptograficamente auditável via **HMAC-SHA256(serverSeed, clientSeed + nonce)**.

- Hash do server seed exibido antes de qualquer giro
- Client seed configurável pelo jogador
- Nonce incremental por sessão
- Rota `/verify` para auditar qualquer resultado após revelação do server seed
- **RTP > 95%** validado em simulação de **5 milhões de giros**
- Swagger UI com todos os endpoints documentados em `/docs`

### 🪄 Caldeirão — Símbolo Scatter (feature extra)

O spec original não previa Scatter. Foi adicionado porque **uma slot machine de Halloween sem caldeirão borbulhando é crime**.

O Caldeirão:
- Paga sobre a **aposta total** (não por linha), em qualquer posição da grade
- Concede **8 Free Spins** com 3+ — re-triggável
- Aparece em qualquer reel

**Multiplicadores scatter:**
| 3 Caldeirões | 4 Caldeirões | 5 Caldeirões |
|:---:|:---:|:---:|
| 2× aposta total | 5× | 20× |

> 🎮 **Jogue até conseguir 3 Caldeirões simultaneamente. Haverá uma surpresa bem chocante — literalmente.**
>
> *(O trovão não morde. Mas vai te pegar desprevenido.)*

O efeito combina: flash de tela controlado por `AnimationCurve` + áudio de trovão + partículas, tudo sincronizado pela corrotina `ThunderFlashRoutine` integrada à `HauntedReelsView`.

### 🎵 Áudio — 100% Autoral

O spec dizia: *"Áudio: Nenhum asset de áudio é fornecido. Áudio é diferencial, não requisito."*

Todos os áudios do jogo são **compostos e produzidos do zero**, sem nenhum asset externo.

> 🎵 **Easter egg:** todas as faixas foram compostas no tom de Dó Sustenido Menor que em Cifra teria a sigla **C#m**.
> Porque o projeto inteiro é escrito em **C#**. Coincidência? Nunca.

O sistema de áudio foi além:

- `HauntedAudioManager` com **dual-buffer seamless loop** via `AudioSource.PlayScheduled` + polling de `AudioSettings.dspTime` — baixo gap entre repetições da música
- **Unity Addressables** para carregamento remoto dos clips de áudio (hospedados no GitHub Releases, fora do build principal)
- Controle independente de mute por tipo (Music / SFX) com persistência via `PlayerPrefs`
- Fila de plays pendentes durante o carregamento dos assets

### ✨ Juice & Game Feel

- **Animações de entrada da cena**: sequência DOTween onde cada elemento aparece individualmente, culminando num fade-in do background da slot
- **Paylines iluminadas**: `UIPaylineRenderer` — subclasse de `Graphic` que desenha a linha vencedora no canvas UI com segmentos lit / dim, usando `RectTransformUtility` para converter posições dos símbolos em coordenadas locais
- **Contador de prêmio crescente**: o valor sobe gradualmente até o total ganho durante a animação de win
- **Win level headers**: painel de vitória muda visual conforme o nível (Small / Big / Mega / Jackpot)
- **SymbolIdleFloat**: símbolo flutua suavemente quando em repouso via DOTween Yoyo — para símbolos sem Spine
- **Modo auto-spin**: gira automaticamente, com pausa proporcional ao nível de vitória antes de continuar
- **Partículas** quando ocorre um scatter wins e ficamos no state de free spins
- **Cena de menu** com animações de entrada, logo, e fade para a cena de jogo
- **Fonte Cinzel** em toda a UI (fornecida no pacote de assets e também usada na landing page)

### 🌐 Landing Page (GitHub Pages)

Página temática completa em `gh-pages/index.html`:
- Iframe da build WebGL em **aspect ratio 16:9 fixo** (padding-top: 56.25%)
- Canvas animado com estrelas e morcegos voando (JavaScript puro)
- Paytable completa com sprites reais dos símbolos
- Visualização interativa das 10 paylines com grade 3×5
- Seções de regras e Provably Fair
- Tema Halloween: dark purple/black com laranja e glow animado

---

## 🏗️ Arquitetura

```
haunted-reels/
├── haunted-reels-backend/         # Node.js + Fastify
│   └── src/
│       ├── engine/
│       │   ├── spinEngine.js      # Avaliação de paylines, Wild, Scatter, Free Spins
│       │   └── config.js          # Paytable, reel strips, payline paths
│       ├── provablyfair/
│       │   └── pfManager.js       # HMAC-SHA256, sessões, nonces
│       └── routes/
│           ├── spin.js            # POST /spin
│           ├── session.js         # GET + POST /session
│           └── verify.js          # POST /verify
│
├── haunted-reels-unity/           # Unity 2022.3 LTS
│   └── Assets/Haunted Reels Game/
│       ├── Scripts/
│       │   ├── Audio/
│       │   │   └── HauntedAudioManager.cs   # Dual-buffer loop + Addressables
│       │   ├── Presenter/
│       │   │   ├── SlotMachinePresenter.cs  # Orquestra spin / UI
│       │   │   └── SessionPresenter.cs      # Sessão e saldo
│       │   ├── View/
│       │   │   ├── HauntedReelsView.cs      # View principal (ISlotMachineView)
│       │   │   ├── UIPaylineRenderer.cs     # Custom Graphic para paylines
│       │   │   ├── GameSceneEntrance.cs     # Animações de entrada
│       │   │   └── SymbolIdleFloat.cs       # Float idle via DOTween
│       │   ├── Model/
│       │   │   └── SessionModel.cs          # Estado local da sessão
│       │   └── Network/
│       │       ├── ApiClient.cs             # HTTP para o backend
│       │       └── Dtos.cs                  # Contratos da API
│       └── SpineAnims/                      # HalloweenCreature (witch, pumpkin, skull)
│
└── gh-pages/                      # Landing page GitHub Pages
    ├── index.html
    └── assets/                    # Sprites exportados do Unity
```

### Padrão MVP (Model-View-Presenter)

O projeto segue o padrão fornecido pelo pacote `com.slot-engine`:

| Camada | Classe | Responsabilidade |
|--------|--------|-----------------|
| **Model** | `SessionModel` | Estado de sessão, saldo, free spins |
| **View** | `HauntedReelsView` | Implementa `ISlotMachineView`, zero lógica de negócio |
| **Presenter** | `SlotMachinePresenter` | Orquestra backend ↔ view |

O pacote SlotEngine fornece as abstrações base (`AudioManager`, `ReelStrip`, `SymbolLibrary`) que foram estendidas sem modificar o código do pacote.

### Decisões notáveis
- **Precisão monetária**: `Math.round(v * 100) / 100` (round2) em todas as operações do backend; `(float)Math.Round(v, 2)` no Unity — sem truncamento de centavos
- **Spine + fallback**: `SkeletonGraphic` detectado em runtime; se ausente, DOTween assume automaticamente
- **Seamless audio loop**: `AudioSettings.dspTime` em vez de `WaitForSecondsRealtime` para evitar drift de relógio
- **Addressables**: clips de áudio fora do build principal — carregados sob demanda do GitHub Releases

---

## ⏱️ Tempo de Desenvolvimento

| Feature | Tempo |
|---------|-------|
| Configuração do projeto, MVP, integração SlotEngine | ~3h |
| Cena de jogo: UI, HUD, painel de aposta, painel de win | ~3h |
| Integração Spine (skins + animações idle/die) | ~2h |
| Animações de símbolo e juice (DOTween, partículas, win counter) | ~3h |
| Backend: engine de spin, paylines, paytable, Scatter, Free Spins | ~5h |
| Sistema Provably Fair (HMAC-SHA256, sessões, `/verify`) | ~3h |
| Integração Unity ↔ Backend (ApiClient, DTOs, fluxo completo) | ~3h |
| Sistema de áudio autoral + HauntedAudioManager + loop seamless | ~4h |
| Efeito de trovão (ThunderFlash + scatter juice + paylines iluminadas) | ~2h |
| UIPaylineRenderer (custom Graphic) | ~2h |
| Animações de entrada de cena + cena de menu | ~2h |
| Ajustes de RTP + simulação 5M spins + correções de precisão float | ~3h |
| Unity Addressables (setup, loader, injeção em runtime) | ~2h |
| Landing page GitHub Pages (design temático completo) | ~3h |
| **Total** | **~40h** |

---

## 🛠️ Stack

| Camada | Tecnologia |
|--------|-----------|
| Game Engine | Unity 2022.3 LTS |
| Linguagem | C# |
| Animações 2D | Spine Unity Runtime 4.2 |
| Tweening | DOTween |
| Async Unity | UniTask (Cysharp) |
| Backend | Node.js 20 + Fastify 4 |
| Criptografia | HMAC-SHA256 (Node `crypto` nativo) |
| API Docs | Swagger UI via `@fastify/swagger` |
| Deploy Backend | Local com tunneling via Ngrok|
| Deploy Frontend | GitHub Pages |
| Asset Streaming | Unity Addressables 1.21 + GitHub Releases |

---

*Feito com 🎃, muito DOTween e C#m — a única tonalidade que programa e compõe ao mesmo tempo.*
