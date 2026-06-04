const {
  REELS,
  PAYTABLE,
  PAYLINES,
  SCATTER_MULTIPLIERS,
  WILD_ID,
  SCATTER_ID,
} = require('./config');

function buildWindow(stopPositions) {
  // stopPositions: [int x5] cada um 0..29
  const grid = [];
  for (let col = 0; col < 5; col++) {
    const reel = REELS[col];
    const stop = ((stopPositions[col] % reel.length) + reel.length) % reel.length;
    const colSymbols = [];
    for (let r = 0; r < 3; r++) {
      colSymbols.push(reel[(stop + r) % reel.length]);
    }
    grid.push(colSymbols);
  }
  return grid; // grid[col][row]
}

// H1 = ID 0 — usado como fallback para linha toda de wilds
const H1_ID = 0;

function evaluateLines(grid, betPerLine) {
  const lineWins = [];

  for (let i = 0; i < PAYLINES.length; i++) {
    const pl = PAYLINES[i];
    const symbols = pl.path.map((rowIdx, colIdx) => grid[colIdx][rowIdx]);

    // Primeiro símbolo não-scatter da linha determina o base
    let baseIdx = -1;
    for (let k = 0; k < symbols.length; k++) {
      if (symbols[k] !== SCATTER_ID) {
        baseIdx = k;
        break;
      }
    }
    if (baseIdx === -1) continue;

    let baseSymbol = symbols[baseIdx];
    if (baseSymbol === WILD_ID) {
      const nonWild = symbols.find((s) => s !== SCATTER_ID && s !== WILD_ID);
      // Linha só de wilds paga como H1 (maior símbolo)
      baseSymbol = nonWild !== undefined ? nonWild : H1_ID;
    }

    // Conta consecutivos da esquerda enquanto símbolo === base ou Wild
    let count = 0;
    for (let k = 0; k < symbols.length; k++) {
      const s = symbols[k];
      if (s === baseSymbol || s === WILD_ID) count++;
      else break;
    }

    if (count >= 3 && PAYTABLE[baseSymbol]) {
      const multiplier = PAYTABLE[baseSymbol][count - 3] || 0;
      const coins    = round2(multiplier * betPerLine);
      const cells    = pl.path.slice(0, count).map((row, colIdx) => [colIdx, row]);
      const linePath = pl.path.map((row, colIdx) => [colIdx, row]); // caminho completo das 5 colunas
      lineWins.push({ lineId: pl.id, lineName: pl.name, symbolId: baseSymbol, count, multiplier, coins, cells, linePath });
    }
  }

  const lineWinTotal = round2(lineWins.reduce((s, w) => s + w.coins, 0));
  return { lineWins, lineWinTotal };
}

function countScatters(grid) {
  let c = 0;
  for (let col = 0; col < grid.length; col++) {
    for (let row = 0; row < grid[col].length; row++) {
      if (grid[col][row] === SCATTER_ID) c++;
    }
  }
  return c;
}

const round2 = v => Math.round(v * 100) / 100;

function evaluateSpin(stopPositions, betPerLine = 1) {
  const totalBet = round2(betPerLine * PAYLINES.length);
  const grid = buildWindow(stopPositions);

  const { lineWins, lineWinTotal } = evaluateLines(grid, betPerLine);
  const scatterCount = countScatters(grid);

  const scatterPositions = [];
  for (let col = 0; col < grid.length; col++) {
    for (let row = 0; row < grid[col].length; row++) {
      if (grid[col][row] === SCATTER_ID) scatterPositions.push([col, row]);
    }
  }

  const scatterCoins = (scatterCount in SCATTER_MULTIPLIERS)
    ? round2(SCATTER_MULTIPLIERS[scatterCount] * totalBet)
    : 0;

  const triggerFreeSpins = scatterCount >= 3;
  const freeSpinsAwarded = triggerFreeSpins ? 8 : 0;

  const totalWin = round2(lineWinTotal + scatterCoins);

  let winLevel = 'none';
  if (totalWin > 0)                   winLevel = 'small';
  if (totalWin >= totalBet * 3)       winLevel = 'big';
  if (totalWin >= totalBet * 10)      winLevel = 'mega';
  if (totalWin >= totalBet * 25)      winLevel = 'jackpot';

  return {
    stopPositions,
    grid,
    lineWins,
    lineWinTotal,
    scatterCount,
    scatterPositions,
    scatterCoins,
    triggerFreeSpins,
    freeSpinsAwarded,
    totalBet,
    totalWin,
    winLevel,
  };
}

module.exports = { buildWindow, evaluateLines, countScatters, evaluateSpin };
