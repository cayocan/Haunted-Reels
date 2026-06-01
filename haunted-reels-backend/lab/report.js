'use strict';

const fs = require('fs');
const path = require('path');

function save(r, rtp, hitRate, scatterRate, freeSpinRate, elapsed, SYMBOLS) {
  const jsonPayload = {
    timestamp: new Date().toISOString(),
    spins: r.spins,
    betPerLine: r.bet,
    totalBet: r.spins * r.bet * 15,
    totalWin: r.totalWin,
    rtp: parseFloat(rtp.toFixed(4)),
    hitRate: parseFloat(hitRate.toFixed(4)),
    scatterTriggerRate: parseFloat(scatterRate.toFixed(4)),
    freeSpinRate: parseFloat(freeSpinRate.toFixed(4)),
    winLevelDistribution: r.winLevelCounts,
    symbolHits: Object.fromEntries(SYMBOLS.map((name, i) => [name, r.symbolHits[i] || 0])),
    elapsedSeconds: parseFloat(elapsed),
  };

  const jsonPath = path.join(__dirname, 'report.json');
  fs.writeFileSync(jsonPath, JSON.stringify(jsonPayload, null, 2));
  console.log(`Relatório JSON salvo em ${jsonPath}`);

  // CSV: uma linha por métrica
  const csvLines = [
    'metric,value',
    `rtp_pct,${rtp.toFixed(4)}`,
    `total_spins,${r.spins}`,
    `hit_rate_pct,${hitRate.toFixed(4)}`,
    `scatter_trigger_rate_pct,${scatterRate.toFixed(4)}`,
    `free_spin_rate_pct,${freeSpinRate.toFixed(4)}`,
    ...Object.entries(r.winLevelCounts).map(([k, v]) => `winlevel_${k},${v}`),
    ...SYMBOLS.map((name, i) => `symbol_hits_${name},${r.symbolHits[i] || 0}`),
    `elapsed_s,${elapsed}`,
  ];
  const csvPath = path.join(__dirname, 'report.csv');
  fs.writeFileSync(csvPath, csvLines.join('\n'));
  console.log(`Relatório CSV salvo em ${csvPath}`);
}

module.exports = { save };
