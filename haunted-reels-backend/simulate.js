// Simulação com reels frescos por spin — distribuição teórica verdadeira.
// Gera um novo layout de reel a cada spin (mantém correlação de 3 linhas
// consecutivas por coluna, mas elimina variância entre execuções).
//
// Uso: node simulate.js [spins] [betTotal]

const { evaluateLines, countScatters } = require('./src/engine/spinEngine');
const { REEL_WEIGHTS, PAYLINES, SCATTER_MULTIPLIERS, SYMBOLS, SYMBOL_IDS } = require('./src/engine/config');
const crypto = require('crypto');

const SPINS     = parseInt(process.argv[2])   || 5_000_000;
const BET_TOTAL = parseFloat(process.argv[3]) || 10;
const BET_LINE  = BET_TOTAL / PAYLINES.length;
const SCATTER_ID = SYMBOL_IDS.SCATTER;
const MAX_FS    = 16;

// expande os pesos num array de símbolos e embaralha (Fisher-Yates com Math.random)
function buildReel(weights) {
  const arr = [];
  for (let sym = 0; sym < weights.length; sym++)
    for (let k = 0; k < weights[sym]; k++)
      arr.push(sym);
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const t = arr[i]; arr[i] = arr[j]; arr[j] = t;
  }
  return arr;
}

// para o grid lemos 3 posições consecutivas por reel (correlação real)
function buildGrid(reels) {
  const grid = [];
  for (let col = 0; col < reels.length; col++) {
    const reel = reels[col];
    const stop = Math.floor(Math.random() * reel.length);
    const colSyms = [];
    for (let r = 0; r < 3; r++)
      colSyms.push(reel[(stop + r) % reel.length]);
    grid.push(colSyms);
  }
  return grid;
}

function spinOnce(betLine) {
  const reels = REEL_WEIGHTS.map(buildReel); // reel fresco a cada spin
  const grid  = buildGrid(reels);
  const { lineWins, lineWinTotal } = evaluateLines(grid, betLine);
  const scatterCnt   = countScatters(grid);
  const scatterCoins = (SCATTER_MULTIPLIERS[scatterCnt] ?? 0) * betLine * PAYLINES.length;
  const triggerFS    = scatterCnt >= 3;
  const totalWin     = lineWinTotal + scatterCoins;
  const totalBet     = betLine * PAYLINES.length;
  let winLevel = 'none';
  if (totalWin > 0)             winLevel = 'small';
  if (totalWin >= totalBet * 3) winLevel = 'big';
  if (totalWin >= totalBet * 10)winLevel = 'mega';
  if (totalWin >= totalBet * 25)winLevel = 'jackpot';
  return { lineWins, scatterCnt, scatterCoins, triggerFS, totalWin, winLevel };
}

// ── Acumuladores ──────────────────────────────────────────────────────────────

let totalWagered = 0, totalWon = 0, freeSpinWon = 0, freeSpinTotal = 0, fsTriggers = 0;
let maxWin = 0, maxWinSpin = 0;
const winLevelCount = { none:0, small:0, big:0, mega:0, jackpot:0 };
const symbolWins    = {};
const scatterHits   = {};

console.log(`\nSimulando ${SPINS.toLocaleString()} spins | aposta = ${BET_TOTAL} | betPerLine = ${BET_LINE.toFixed(4)}`);
console.log(`Reels: frescos por spin (distribuição teórica)\n`);
const t0 = Date.now();

for (let s = 0; s < SPINS; s++) {
  const spin = spinOnce(BET_LINE);
  totalWagered += BET_TOTAL;
  totalWon     += spin.totalWin;
  if (spin.totalWin > maxWin) { maxWin = spin.totalWin; maxWinSpin = s + 1; }
  winLevelCount[spin.winLevel]++;
  scatterHits[spin.scatterCnt] = (scatterHits[spin.scatterCnt] || 0) + 1;
  for (const lw of spin.lineWins) {
    if (!symbolWins[lw.symbolId]) symbolWins[lw.symbolId] = { hits: 0, coins: 0 };
    symbolWins[lw.symbolId].hits++;
    symbolWins[lw.symbolId].coins += lw.coins;
  }
  if (spin.triggerFS) {
    fsTriggers++;
    for (let f = 0; f < MAX_FS; f++) {
      const fs = spinOnce(BET_LINE);
      totalWon    += fs.totalWin;
      freeSpinWon += fs.totalWin;
      freeSpinTotal++;
      // sem re-trigger em free spins
    }
  }
}

const elapsed = ((Date.now() - t0) / 1000).toFixed(2);
const rtp     = (totalWon / totalWagered * 100).toFixed(4);
const rtpBase = ((totalWon - freeSpinWon) / totalWagered * 100).toFixed(4);
const rtpFS   = (freeSpinWon / totalWagered * 100).toFixed(4);

console.log('═'.repeat(62));
console.log('  RELATÓRIO DE SIMULAÇÃO — HAUNTED REELS');
console.log('═'.repeat(62));
console.log(`  Spins simulados    : ${SPINS.toLocaleString()}`);
console.log(`  Aposta total/spin  : ${BET_TOTAL}  |  betPerLine: ${BET_LINE.toFixed(4)}`);
console.log(`  Total apostado     : ${totalWagered.toLocaleString()}`);
console.log(`  Total ganho        : ${totalWon.toFixed(2)}`);
console.log(`  RTP total          : ${rtp}%`);
console.log(`    ├ spins pagos     : ${rtpBase}%`);
console.log(`    └ free spins      : ${rtpFS}%`);
console.log(`  Maior vitória      : ${maxWin.toFixed(2)}  (spin #${maxWinSpin.toLocaleString()})`);
console.log(`  Free spin triggers : ${fsTriggers.toLocaleString()}  (${(fsTriggers/SPINS*100).toFixed(3)}%)`);
console.log(`  Free spins jogados : ${freeSpinTotal.toLocaleString()}  (${MAX_FS} por trigger)`);
console.log(`  Tempo              : ${elapsed}s`);
console.log('');
console.log('─── Distribuição de Nível de Vitória ───');
for (const [level, count] of Object.entries(winLevelCount)) {
  const pct = (count / SPINS * 100).toFixed(3);
  console.log(`  ${level.padEnd(8)}: ${count.toLocaleString().padStart(12)}  (${pct}%)`);
}
console.log('');
console.log('─── Vitórias por Símbolo ───');
for (const [id, { hits, coins }] of Object.entries(symbolWins).sort((a, b) => +a[0] - +b[0])) {
  const name = SYMBOLS[id] || `id_${id}`;
  const pct  = (hits / SPINS * 100).toFixed(3);
  console.log(`  ${name.padEnd(20)}: ${hits.toLocaleString().padStart(10)} hits  (${pct}%)  coins: ${coins.toFixed(2)}`);
}
console.log('');
console.log('─── Scatters ───');
for (let n = 0; n <= 5; n++) {
  const c   = scatterHits[n] || 0;
  const pct = (c / SPINS * 100).toFixed(3);
  const tag = n >= 3 ? ' ★' : '  ';
  console.log(`${tag}  ${n} scatter(s): ${c.toLocaleString().padStart(12)}  (${pct}%)`);
}
console.log('═'.repeat(62));
