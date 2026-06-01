'use strict';

const { Worker, isMainThread, parentPort, workerData } = require('worker_threads');
const os = require('os');
const crypto = require('crypto');
const path = require('path');

// ─── Worker thread ───────────────────────────────────────────────────────────
if (!isMainThread) {
  // Lab importa apenas engine e config — sem server.js
  const engine = require('../src/engine/spinEngine');
  const config = require('../src/engine/config');

  const { spins, bet } = workerData;

  const REEL_LEN = 30;
  const LINES = config.PAYLINES.length;

  // Estatísticas acumuladas
  let totalWin = 0;
  let spinsWithHit = 0;
  let scatterTriggers = 0;
  let freeSpinSpins = 0;

  const winLevelCounts = { none: 0, small: 0, big: 0, mega: 0, jackpot: 0 };
  const symbolHits = new Array(9).fill(0); // índice = symbolId

  for (let i = 0; i < spins; i++) {
    // crypto.randomBytes para CSPRNG puro
    const buf = crypto.randomBytes(20); // 5 reels × 4 bytes
    const stops = [];
    for (let r = 0; r < 5; r++) {
      stops.push(buf.readUInt32BE(r * 4) % REEL_LEN);
    }

    const res = engine.evaluateSpin(stops, bet);

    totalWin += res.totalWin;
    winLevelCounts[res.winLevel]++;

    if (res.lineWins.length > 0) spinsWithHit++;
    if (res.triggerFreeSpins) {
      scatterTriggers++;
      freeSpinSpins += res.freeSpinsAwarded;
    }

    // Conta aparições de cada símbolo em todas as 15 células da grid (5 reels × 3 rows)
    // Usar a grid, não lineWins, para que Wild, Scatter e Blank também sejam contabilizados
    for (const col of res.grid) {
      for (const sym of col) {
        if (sym >= 0 && sym < symbolHits.length) {
          symbolHits[sym]++;
        }
      }
    }
  }

  parentPort.postMessage({ totalWin, spins, spinsWithHit, scatterTriggers, freeSpinSpins, winLevelCounts, symbolHits });
  process.exit(0);
}

// ─── Main thread ─────────────────────────────────────────────────────────────
function run(spins, workers, bet) {
  return new Promise((resolve) => {
    const per = Math.floor(spins / workers);
    let finished = 0;

    // Accumulators
    let totalWin = 0;
    let spinsWithHit = 0;
    let scatterTriggers = 0;
    let freeSpinSpins = 0;
    const winLevelCounts = { none: 0, small: 0, big: 0, mega: 0, jackpot: 0 };
    const symbolHits = new Array(9).fill(0);

    for (let i = 0; i < workers; i++) {
      const wSpins = i === workers - 1 ? spins - per * (workers - 1) : per;
      const w = new Worker(__filename, { workerData: { spins: wSpins, bet } });

      w.on('message', (m) => {
        totalWin += m.totalWin;
        spinsWithHit += m.spinsWithHit;
        scatterTriggers += m.scatterTriggers;
        freeSpinSpins += m.freeSpinSpins;
        for (const k of Object.keys(winLevelCounts)) winLevelCounts[k] += m.winLevelCounts[k];
        for (let s = 0; s < symbolHits.length; s++) symbolHits[s] += m.symbolHits[s];
      });

      w.on('exit', () => {
        finished++;
        if (finished === workers) {
          resolve({ totalWin, spins, spinsWithHit, scatterTriggers, freeSpinSpins, winLevelCounts, symbolHits, bet });
        }
      });
    }
  });
}

function parseArgs() {
  const args = process.argv.slice(2);
  const parsed = {};
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a.startsWith('--')) {
      const k = a.replace(/^--/, '');
      const v = args[i + 1] && !args[i + 1].startsWith('--') ? args[++i] : 'true';
      parsed[k] = v;
    }
  }
  return parsed;
}

if (require.main === module) {
  const argv = parseArgs();
  const spins   = Number(argv.spins)   || 100000;
  const workers = Number(argv.workers) || Math.max(1, os.cpus().length - 1);
  const bet     = Number(argv.bet)     || 1;

  const SYMBOLS = ['Husky', 'Golden', 'Shiba', 'Pug', 'Beagle', 'Dachshund', 'Wild', 'Scatter', 'Blank'];
  const LINES   = require('../src/engine/config').PAYLINES.length;

  console.log(`\nIniciando simulação: ${spins.toLocaleString()} spins | ${workers} workers | betPerLine=${bet}`);
  const t0 = Date.now();

  run(spins, workers, bet).then((r) => {
    const elapsed = ((Date.now() - t0) / 1000).toFixed(2);
    const totalBet = r.spins * r.bet * LINES;
    const rtp = (r.totalWin / totalBet) * 100;
    const hitRate = (r.spinsWithHit / r.spins) * 100;
    const scatterRate = (r.scatterTriggers / r.spins) * 100;
    const freeSpinRate = (r.freeSpinSpins / r.spins) * 100;

    const pct = (n) => ((n / r.spins) * 100).toFixed(2) + '%';

    console.log(`\n${'─'.repeat(50)}`);
    console.log(`RTP calculado:        ${rtp.toFixed(4)}%   (meta: 96%)`);
    console.log(`Total spins:          ${r.spins.toLocaleString()}`);
    console.log(`Hit rate (linhas):    ${hitRate.toFixed(2)}%`);
    console.log(`Scatter trigger rate: ${scatterRate.toFixed(3)}%`);
    console.log(`Free spin rate:       ${freeSpinRate.toFixed(3)}% (spins gratuitos acumulados)`);
    console.log(`\nWin level distribution:`);
    for (const lvl of ['none', 'small', 'big', 'mega', 'jackpot']) {
      console.log(`  ${lvl.padEnd(10)}: ${pct(r.winLevelCounts[lvl])}`);
    }
    console.log(`\nTop winning symbols:`);
    for (let s = 0; s < 6; s++) {
      console.log(`  ${SYMBOLS[s].padEnd(12)}: ${r.symbolHits[s].toLocaleString()} hits`);
    }
    console.log(`\nTempo de execução: ${elapsed}s`);
    console.log(`${'─'.repeat(50)}\n`);

    // Salvar relatórios
    const report = require('./report');
    report.save(r, rtp, hitRate, scatterRate, freeSpinRate, elapsed, SYMBOLS);

    process.exit(0);
  });
}
