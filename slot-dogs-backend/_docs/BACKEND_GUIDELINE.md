# Dog Slot — Backend Implementation Guideline

> Documento de referência para a IA implementar o backend Node.js do slot machine.
> Leia este arquivo inteiro antes de escrever qualquer linha de código.

---

## 1. Visão geral

Backend Node.js responsável por **todo o cálculo do jogo**. O frontend Unity WebGL é apenas um renderer — ele envia intenções e exibe resultados. Nenhuma lógica de jogo reside no cliente.

- **Sem banco de dados** — estado de sessão em memória (Map)
- **Sem autenticação** — sessão identificada por `sessionId` gerado no cliente
- **Sem persistência** — ao reiniciar o servidor, sessões são perdidas (comportamento esperado)
- **Moedas**: cada sessão começa com 100 moedas. **Por ora o giro não desconta moedas** — a infraestrutura de custo já deve existir no código (campo `coinCost`, função `deductCoins`) mas comentada/desabilitada com `TODO: ENABLE_COST`. Quando o produto evoluir, basta descomentar.

---

## 2. Stack

| Camada | Tecnologia | Motivo |
|---|---|---|
| Runtime | Node.js 20+ | LTS, crypto nativo |
| Framework | **Fastify** | Mais rápido que Express, schema validation built-in |
| Logger | **Pino** | JSON estruturado, baixíssimo overhead, pretty-print no dev |
| RNG | `crypto.randomBytes` nativo | True CSPRNG, zero dependências externas |
| Provably Fair | HMAC-SHA256 (crypto nativo) | Auditável, sem libs terceiras |
| CORS | `@fastify/cors` | Necessário para WebGL |
| Live logs | SSE (`text/event-stream`) | Ver logs no browser sem ferramenta externa |
| Lab de testes | Worker Threads (nativo) | Paraleliza milhões de spins sem libs |

**Dependências de produção:** `fastify`, `@fastify/cors`, `pino`, `pino-pretty`
**Dev:** `nodemon`

---

## 3. Estrutura de pastas

```
slot-backend/
├── src/
│   ├── engine/
│   │   ├── config.js          # símbolos, paytable, reel strips, paylines
│   │   └── spinEngine.js      # lógica pura de spin (sem HTTP, sem I/O)
│   ├── rng/
│   │   └── rng.js             # randomFloat, deriveFloatsFromSeed, hash
│   ├── provablyfair/
│   │   └── pfManager.js       # createSession, getSpinFloats, rotateSeed, verify
│   ├── routes/
│   │   ├── session.js         # POST /session, POST /session/:id/seed, GET /session/:id
│   │   ├── spin.js            # POST /spin
│   │   ├── verify.js          # POST /verify
│   │   └── logs.js            # GET /logs/stream (SSE)
│   ├── logger/
│   │   └── logger.js          # instância Pino + broadcast para SSE
│   └── server.js              # bootstrap Fastify, registra plugins e rotas
├── lab/
│   ├── simulate.js            # worker threads, N milhões de spins
│   └── report.js              # agrega resultados, gera CSV e JSON
├── package.json
└── .env.example
```

---

## 4. Configuração do jogo (config.js)

### 4.1 Símbolos

| ID | Nome | Tier |
|---|---|---|
| 0 | Husky Siberiano | S |
| 1 | Golden Retriever | A |
| 2 | Shiba Inu | A |
| 3 | Pug | B |
| 4 | Beagle | B |
| 5 | Dachshund | C |
| 6 | Patinha Dourada | WILD |
| 7 | Ossinho | SCATTER |
| 8 | Blank | — |

### 4.2 Paytable (multiplicador × aposta por linha)

| Símbolo | 3× | 4× | 5× |
|---|---|---|---|
| Husky | 15 | 75 | 500 |
| Golden | 8 | 40 | 200 |
| Shiba | 6 | 30 | 150 |
| Pug | 4 | 20 | 80 |
| Beagle | 3 | 15 | 60 |
| Dachshund | 2 | 10 | 40 |

**Wild** substitui qualquer raça (não substitui Scatter). Aparece apenas nos reels 2, 3 e 4.
Linha composta só de Wilds → paga como Husky.

**Scatter pays** (multiplicador × aposta total, qualquer posição na grade):

| Scatter count | Multiplicador |
|---|---|
| 3 | 2× |
| 4 | 5× |
| 5 | 20× |

3 ou mais Scatters em qualquer reel → trigger de **8 free spins**.

### 4.3 Reel strips (30 posições por reel)

| Símbolo | Reel 1 | Reel 2 | Reel 3 | Reel 4 | Reel 5 |
|---|---|---|---|---|---|
| Husky | 1 | 1 | 1 | 1 | 1 |
| Golden | 2 | 2 | 2 | 2 | 2 |
| Shiba | 2 | 2 | 2 | 2 | 2 |
| Pug | 3 | 3 | 3 | 3 | 3 |
| Beagle | 4 | 4 | 4 | 4 | 4 |
| Dachshund | 7 | 7 | 7 | 7 | 7 |
| Wild | 0 | 2 | 2 | 2 | 0 |
| Scatter | 2 | 1 | 1 | 1 | 2 |
| Blank | 9 | 8 | 8 | 8 | 9 |
| **Total** | **30** | **30** | **30** | **30** | **30** |

O strip é construído programaticamente expandindo os pesos. Cada reel é um array de IDs embaralhado (Fisher-Yates) uma única vez na inicialização do servidor — o stop position é gerado pelo RNG a cada spin.

### 4.4 Linhas de pagamento (15 linhas fixas)

Grade: 5 colunas × 3 linhas. Índices de linha: `0` = topo, `1` = meio, `2` = baixo.
Cada linha é um array de 5 índices de linha, um por reel.

| ID | Nome | Path |
|---|---|---|
| 1 | Linha do meio | [1,1,1,1,1] |
| 2 | Linha de cima | [0,0,0,0,0] |
| 3 | Linha de baixo | [2,2,2,2,2] |
| 4 | V invertido | [0,1,2,1,0] |
| 5 | V normal | [2,1,0,1,2] |
| 6 | Diagonal ↘ | [0,0,1,2,2] |
| 7 | Diagonal ↗ | [2,2,1,0,0] |
| 8 | Z cima→baixo | [0,0,1,2,2] |
| 9 | Z invertido | [2,2,1,0,0] |
| 10 | Escada ↘ | [0,1,1,2,2] |
| 11 | Escada ↗ | [2,1,1,0,0] |
| 14 | Onda suave ↘ | [0,1,1,1,2] |
| 15 | Onda suave ↗ | [2,1,1,1,0] |
| 16 | Topo-meio-baixo | [0,1,2,1,0] |
| 20 | Cruzada central | [1,0,1,2,1] |

---

## 5. Lógica do spin (spinEngine.js)

Este módulo é **puro** — sem efeitos colaterais, sem I/O. Recebe inputs, retorna resultado. Isso permite usar o mesmo código no servidor HTTP e no lab de testes.

### 5.1 Fluxo de um spin

```
stopPositions[5]  →  buildWindow()  →  grid[5][3]
                                          │
                                    evaluateLines()   ← PAYLINES
                                    countScatters()
                                    calcWinLevel()
                                          │
                                    SpinResult object
```

### 5.2 buildWindow(stopPositions)

- Recebe 5 stop positions (índice no reel strip, 0–29)
- Para cada reel, extrai 3 símbolos consecutivos (com wrap circular)
- Retorna `grid[col][row]` = symbolId

### 5.3 evaluateLines(grid, betPerLine)

Para cada uma das 15 paylines:
1. Pega o símbolo da posição `grid[col][path[col]]` para cada col 0–4
2. O primeiro símbolo define o tipo da linha (ignora se for Blank ou Scatter)
3. Wild estende a sequência sem quebrar
4. Conta quantos símbolos consecutivos do mesmo tipo (da esquerda)
5. Mínimo 3 para pagar. Consulta PAYTABLE[symbolId][count - 3]
6. Linha de Wilds puros → paga como Husky
7. Acumula `lineWins[]` com `{ lineId, symbolId, count, multiplier, coins }`

### 5.4 countScatters(grid)

Conta todas as ocorrências de SCATTER (id 7) na grade inteira (não por linha).

### 5.5 SpinResult (objeto de retorno completo)

```js
{
  stopPositions: [int, int, int, int, int],
  grid: [[sym,sym,sym], ...],   // 5 colunas × 3 linhas
  lineWins: [
    { lineId, lineName, symbolId, count, multiplier, coins }
  ],
  lineWinTotal: int,
  scatterCount: int,
  scatterCoins: int,
  triggerFreeSpins: bool,
  freeSpinsAwarded: int,        // 8 se trigger, 0 caso contrário
  totalBet: int,                // betPerLine × 15 linhas
  totalWin: int,
  winLevel: 'none' | 'small' | 'big' | 'mega' | 'jackpot',
  // winLevel thresholds:
  //   small   → totalWin > 0
  //   big     → totalWin >= totalBet × 5
  //   mega    → totalWin >= totalBet × 20
  //   jackpot → totalWin >= totalBet × 50
}
```

---

## 6. RNG (rng.js)

```js
// Retorna float em [0, 1) com 32 bits de entropia
function randomFloat() {
  const buf = crypto.randomBytes(4);
  return buf.readUInt32BE(0) / 0x100000000;
}

// Retorna N floats em um único syscall (mais eficiente no lab)
function randomFloats(n) { ... }

// Provably Fair: deriva 5 floats de reel a partir dos seeds
// combinação: HMAC-SHA256(serverSeed, `${clientSeed}:${nonce}:${reelIndex}`)
function deriveFloatsFromSeed(serverSeed, clientSeed, nonce) { ... }

function generateServerSeed()      // 32 bytes hex aleatórios
function hashServerSeed(seed)      // SHA256 hex
function verifySpin(serverSeed, clientSeed, nonce, claimedStops) // bool
```

---

## 7. Provably Fair (pfManager.js)

### Fluxo completo

```
1. Cliente chama POST /session
   → servidor gera serverSeed, retorna serverSeedHash (commitment)

2. Cliente define seu clientSeed: POST /session/:id/seed { clientSeed }

3. A cada spin:
   → servidor deriva floats via HMAC-SHA256(serverSeed, clientSeed:nonce:reelIdx)
   → nonce incrementa
   → resultado inclui { nonce, serverSeedHash, clientSeed }

4. Cliente pode rotacionar seeds a qualquer momento: POST /session/:id/rotate
   → servidor revela serverSeed atual (agora auditável)
   → gera novo serverSeed, retorna novo hash

5. Auditoria: POST /verify { serverSeed, clientSeed, nonce, stops }
   → servidor recomputa e confirma true/false
```

### Estado de sessão em memória

```js
{
  sessionId: string,
  coins: 100,                   // começa com 100
  serverSeed: string,           // NUNCA enviado ao cliente enquanto ativo
  serverSeedHash: string,       // enviado ao cliente como commitment
  clientSeed: string | null,
  nonce: int,                   // incrementa a cada spin
  revealedSeeds: [],            // histórico para auditoria
  freeSpinsRemaining: 0,        // contador de free spins ativos
  betPerLine: 1,                // aposta atual (futuramente configurável)
}
```

---

## 8. Rotas HTTP

### POST `/session`

Cria nova sessão. Retorna commitment inicial.

**Response:**
```json
{
  "sessionId": "uuid-v4",
  "serverSeedHash": "sha256hex",
  "coins": 100,
  "betPerLine": 1
}
```

---

### POST `/session/:id/seed`

Define ou atualiza o clientSeed antes de girar.

**Body:** `{ "clientSeed": "string-qualquer" }`
**Response:** `{ "ok": true }`

---

### GET `/session/:id`

Retorna estado atual da sessão (sem revelar serverSeed).

**Response:**
```json
{
  "sessionId": "...",
  "coins": 100,
  "betPerLine": 1,
  "serverSeedHash": "...",
  "clientSeed": "...",
  "nonce": 5,
  "freeSpinsRemaining": 0
}
```

---

### POST `/spin`

**O endpoint principal.** Executa um giro.

**Body:**
```json
{
  "sessionId": "uuid",
  "betPerLine": 1
}
```

**Lógica interna:**
1. Valida sessão e clientSeed (retorna 400 se não existir)
2. `// TODO: ENABLE_COST — deductCoins(session, totalBet)` ← desabilitado por ora
3. Gera stops via Provably Fair (`getSpinFloats`)
4. Chama `evaluateSpin(stops, betPerLine)`
5. Adiciona `totalWin` às moedas da sessão
6. Se `triggerFreeSpins` → `session.freeSpinsRemaining += 8`
7. Loga o resultado via Pino (e broadcast SSE)
8. Retorna SpinResult + estado atualizado da sessão

**Response:**
```json
{
  "spin": { ...SpinResult },
  "session": {
    "coins": 112,
    "freeSpinsRemaining": 0,
    "nonce": 6,
    "serverSeedHash": "..."
  },
  "provablyFair": {
    "serverSeedHash": "...",
    "clientSeed": "...",
    "nonce": 5
  }
}
```

**Nota sobre free spins:** Se `session.freeSpinsRemaining > 0`, o spin não deve descontar moedas quando `ENABLE_COST` for ativado. Decrementar `freeSpinsRemaining` antes de executar o spin.

---

### POST `/session/:id/rotate`

Rotaciona seeds. Revela o serverSeed atual.

**Response:**
```json
{
  "revealed": {
    "serverSeed": "hex-agora-revelado",
    "serverSeedHash": "...",
    "clientSeed": "...",
    "nonceRange": [0, 12]
  },
  "newServerSeedHash": "novo-commitment"
}
```

---

### POST `/verify`

Auditoria pública. Qualquer um pode verificar um spin passado.

**Body:**
```json
{
  "serverSeed": "...",
  "clientSeed": "...",
  "nonce": 5,
  "stops": [3, 17, 8, 22, 11]
}
```

**Response:**
```json
{
  "valid": true,
  "recomputedStops": [3, 17, 8, 22, 11]
}
```

---

### GET `/logs/stream`

SSE — stream de logs ao vivo para o browser.

**Headers de resposta:**
```
Content-Type: text/event-stream
Cache-Control: no-cache
Connection: keep-alive
```

Cada evento:
```
data: {"level":"info","time":1234567890,"msg":"spin","sessionId":"...","totalWin":20,"winLevel":"small"}
```

O logger Pino deve ter um transport customizado que, além de escrever no stdout, faz broadcast para todos os clientes SSE conectados.

---

## 9. Logger (logger.js)

- Instância Pino com `level: 'info'`
- Em desenvolvimento (`NODE_ENV=development`): pretty-print via `pino-pretty`
- Em produção: JSON puro no stdout
- Expõe função `broadcastToSSE(logObject)` usada pelo transport
- Todo spin gera um log com campos: `sessionId`, `nonce`, `totalBet`, `totalWin`, `winLevel`, `scatterCount`, `triggerFreeSpins`, `stopPositions`

---

## 10. Lab de testes (lab/simulate.js)

O lab importa diretamente `spinEngine.js` e `config.js` — **sem levantar servidor HTTP**. Usa `crypto.randomBytes` para RNG (não usa Provably Fair, pois o objetivo é calcular RTP estatístico puro).

### Como rodar

```bash
node lab/simulate.js --spins 1000000 --workers 4 --bet 1
```

### Fluxo interno

1. Divide `totalSpins` entre N workers (Worker Threads)
2. Cada worker roda seu chunk em loop puro, acumula estatísticas
3. Main thread agrega resultados
4. Gera relatório no console + salva `lab/report.json` e `lab/report.csv`

### Estatísticas reportadas

```
RTP calculado:        96.XXXX%   (meta: 96%)
Total spins:          1.000.000
Hit rate (linhas):    XX.XX%
Scatter trigger rate: X.XXX%
Free spin rate:       X.XXX%

Win level distribution:
  none:    XX.XX%
  small:   XX.XX%
  big:      X.XX%
  mega:     X.XX%
  jackpot:  X.XX%

Top winning symbols:
  Husky:       XXX hits
  Golden:      XXX hits
  ...

Tempo de execução: X.XXs
```

### Como ajustar o RTP

Se o RTP calculado estiver fora da meta (±0.5%), ajuste os pesos da tabela `REEL_WEIGHTS` em `config.js`:
- RTP alto demais → aumentar Blank, reduzir Husky/Golden
- RTP baixo demais → reduzir Blank, aumentar Beagle/Dachshund
- Rode o lab novamente até convergir

---

## 11. Bootstrap do servidor (server.js)

```js
// Ordem de inicialização:
1. Cria instância Fastify com logger Pino integrado
2. Registra @fastify/cors (origem: * em dev, configurável via env em prod)
3. Registra rotas: session, spin, verify, logs
4. Valida reel strips ao iniciar (cada reel deve ter exatamente 30 símbolos)
5. Imprime no console: porta, RTP alvo, número de linhas, versão
6. Listen na porta do .env (default: 3000)
```

---

## 12. Variáveis de ambiente (.env.example)

```env
PORT=3000
NODE_ENV=development
CORS_ORIGIN=*

# Futuramente:
# ENABLE_COST=false       <- quando true, o spin desconta moedas
# MIN_BET_PER_LINE=1
# MAX_BET_PER_LINE=10
```

---

## 13. Regras de implementação

1. **spinEngine.js é puro** — nenhuma chamada a `require` de módulos com I/O. Só recebe dados, retorna dados. Deve ser testável com `node -e`.

2. **Nunca enviar `serverSeed` ativo ao cliente** — apenas o hash. Só revelar no rotate.

3. **Sem `TODO` no meio da lógica crítica** — o bloco de custo deve ser uma função separada `deductCoins(session, amount)` com comentário `// TODO: ENABLE_COST` acima da chamada, não espalhado pelo código.

4. **Logs em todo spin** — sem exceção. O SSE depende disso.

5. **Validar inputs** — sessionId, betPerLine (int, min 1), clientSeed (string, min 8 chars). Retornar 400 com mensagem clara se inválido.

6. **Grid sempre 5×3** — `grid[col][row]`, col de 0 a 4, row de 0 a 2. Consistência com Unity que lê nessa ordem.

7. **Free spins têm prioridade no decremento** — verificar `freeSpinsRemaining > 0` antes de qualquer lógica de custo.

8. **Lab não importa server.js** — apenas engine e config. Se o lab precisar importar o servidor algo está errado.

---

## 14. Checklist antes de considerar o backend pronto

- [ ] `node -e "require('./src/engine/config').REELS.forEach((r,i) => console.log(i, r.length))"` → todos 30
- [ ] `POST /session` retorna `serverSeedHash` e `coins: 100`
- [ ] `POST /spin` sem clientSeed → retorna 400
- [ ] `POST /spin` com clientSeed → retorna SpinResult completo
- [ ] `GET /session/:id` → coins nunca negativos (custo desabilitado)
- [ ] `POST /verify` com dados corretos → `valid: true`
- [ ] `POST /verify` com stop adulterado → `valid: false`
- [ ] `GET /logs/stream` abre conexão SSE e recebe evento a cada spin
- [ ] `node lab/simulate.js --spins 100000` roda sem erro e imprime RTP
- [ ] RTP calculado com 1M spins está entre 94% e 98%
