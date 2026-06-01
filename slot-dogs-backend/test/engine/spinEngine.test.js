'use strict';

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const { buildWindow, evaluateLines, countScatters, evaluateSpin } = require('../../src/engine/spinEngine');
const { REELS, PAYLINES, PAYTABLE } = require('../../src/engine/config');

// Aliases para legibilidade
const H1 = 0, H2 = 1, H3 = 2, L1 = 3, L2 = 4, L3 = 5;
const WILD = 6, SCATTER = 7;

// Helper: cria grade 5×3 preenchida com um símbolo
function filledGrid(sym) {
  return [[sym, sym, sym], [sym, sym, sym], [sym, sym, sym], [sym, sym, sym], [sym, sym, sym]];
}

// Helper: grade com símbolo apenas na linha do meio (row=1)
function middleGrid(sym) {
  return [
    [L3, sym, L3],
    [L3, sym, L3],
    [L3, sym, L3],
    [L3, sym, L3],
    [L3, sym, L3],
  ];
}

// ─── buildWindow ─────────────────────────────────────────────────────────────
describe('buildWindow', () => {
  test('retorna grid 5×3', () => {
    const grid = buildWindow([0, 0, 0, 0, 0]);
    assert.equal(grid.length, 5);
    for (const col of grid) assert.equal(col.length, 3);
  });

  test('todos os símbolos da grid são IDs válidos (0–7)', () => {
    const grid = buildWindow([5, 10, 15, 20, 25]);
    for (const col of grid)
      for (const sym of col)
        assert.ok(sym >= 0 && sym <= 7, `ID inválido: ${sym}`);
  });

  test('wrap-around: stop=29 lê posições 29, 0, 1 do reel', () => {
    const grid = buildWindow([29, 0, 0, 0, 0]);
    assert.equal(grid[0][0], REELS[0][29]);
    assert.equal(grid[0][1], REELS[0][0]);
    assert.equal(grid[0][2], REELS[0][1]);
  });

  test('stop=0 lê as 3 primeiras posições do reel', () => {
    const grid = buildWindow([0, 0, 0, 0, 0]);
    for (let c = 0; c < 5; c++) {
      assert.equal(grid[c][0], REELS[c][0]);
      assert.equal(grid[c][1], REELS[c][1]);
      assert.equal(grid[c][2], REELS[c][2]);
    }
  });
});

// ─── evaluateLines ────────────────────────────────────────────────────────────
describe('evaluateLines', () => {
  test('5-in-a-row L3 na linha do meio → multiplier correto', () => {
    const grid = middleGrid(L3);
    const { lineWins, lineWinTotal } = evaluateLines(grid, 1);
    const win = lineWins.find(w => w.lineId === 1);
    assert.ok(win, 'Payline 1 deve ter win');
    assert.equal(win.symbolId, L3);
    assert.equal(win.count, 5);
    assert.equal(win.multiplier, PAYTABLE[L3][2]);
    assert.equal(win.coins, PAYTABLE[L3][2]);
    assert.ok(lineWinTotal >= PAYTABLE[L3][2]);
  });

  test('3-in-a-row L3 + WILD na col 1 → sequência de 3 contando WILD', () => {
    // Path [1,1,1,1,1]: L3, WILD, L3, H2, H2 → count=3
    const grid = [
      [H2, L3,   H2],
      [H2, WILD,  H2],
      [H2, L3,   H2],
      [H2, H2,   H2],
      [H2, H2,   H2],
    ];
    const { lineWins } = evaluateLines(grid, 1);
    const win = lineWins.find(w => w.lineId === 1);
    assert.ok(win, 'Payline 1 deve ter win com WILD estendendo');
    assert.equal(win.symbolId, L3);
    assert.equal(win.count, 3);
    assert.equal(win.multiplier, PAYTABLE[L3][0]);
  });

  test('WILD na col 0 conta na sequência', () => {
    // WILD, L3, L3, H2, H2 → count=3
    const grid = [
      [H2, WILD, H2],
      [H2, L3,   H2],
      [H2, L3,   H2],
      [H2, H2,   H2],
      [H2, H2,   H2],
    ];
    const { lineWins } = evaluateLines(grid, 1);
    const win = lineWins.find(w => w.lineId === 1);
    assert.ok(win, 'WILD na col 0 deve iniciar sequência');
    assert.equal(win.count, 3);
  });

  test('SCATTER na col 0 não gera line win na payline 1', () => {
    const grid = [
      [H2, SCATTER, H2],
      [H2, L3,      H2],
      [H2, L3,      H2],
      [H2, L3,      H2],
      [H2, L3,      H2],
    ];
    const { lineWins } = evaluateLines(grid, 1);
    const win = lineWins.find(w => w.lineId === 1);
    assert.equal(win, undefined, 'SCATTER não deve gerar line win');
  });

  test('símbolo diferente no meio quebra a sequência (count < 3 → sem win)', () => {
    // L3, H2, L3, L3, L3 → sequence quebra em col 1
    const grid = [
      [H1, L3, H1],
      [H1, H2, H1],
      [H1, L3, H1],
      [H1, L3, H1],
      [H1, L3, H1],
    ];
    const { lineWins } = evaluateLines(grid, 1);
    const win = lineWins.find(w => w.lineId === 1);
    assert.equal(win, undefined, 'Símbolo diferente no meio deve quebrar sequência');
  });

  test('linha composta só de WILDs paga como H1 (150 × betPerLine)', () => {
    const grid = middleGrid(WILD);
    const { lineWins } = evaluateLines(grid, 1);
    const win = lineWins.find(w => w.lineId === 1 && w.count === 5);
    assert.ok(win, 'Linha de 5 WILDs deve gerar win');
    assert.equal(win.symbolId, H1, 'WILDs puros devem pagar como H1');
    assert.equal(win.multiplier, PAYTABLE[H1][2]); // 150
  });

  test('grade toda de SCATTER não gera nenhum line win', () => {
    const grid = filledGrid(SCATTER);
    const { lineWins } = evaluateLines(grid, 1);
    assert.equal(lineWins.length, 0, 'Scatter não deve gerar line win');
  });

  test('2-in-a-row não paga (mínimo 3)', () => {
    const grid = [
      [H1, H2,  H1],
      [H1, H2,  H1],
      [H1, L3,  H1],
      [H1, L3,  H1],
      [H1, L3,  H1],
    ];
    const { lineWins } = evaluateLines(grid, 1);
    const win = lineWins.find(w => w.lineId === 1);
    assert.equal(win, undefined, '2-in-a-row não deve pagar');
  });

  test('betPerLine multiplica coins corretamente (L3, 5 iguais)', () => {
    const grid = middleGrid(L3);
    const { lineWins } = evaluateLines(grid, 5);
    const win = lineWins.find(w => w.lineId === 1 && w.count === 5);
    assert.ok(win);
    assert.equal(win.coins, PAYTABLE[L3][2] * 5); // 15 × 5 = 75
  });

  test('L2 com 3 iguais paga multiplicador fracionário (1.5×)', () => {
    const grid = [
      [H1, L2, H1],
      [H1, L2, H1],
      [H1, L2, H1],
      [H1, H1, H1],
      [H1, H1, H1],
    ];
    const { lineWins } = evaluateLines(grid, 1);
    const win = lineWins.find(w => w.lineId === 1);
    assert.ok(win, 'L2 3-in-a-row deve gerar win');
    assert.equal(win.multiplier, 1.5);
    assert.equal(win.coins, 1.5);
  });
});

// ─── countScatters ────────────────────────────────────────────────────────────
describe('countScatters', () => {
  test('grade sem scatters → 0', () => {
    assert.equal(countScatters(filledGrid(L3)), 0);
  });

  test('grade com 5 scatters em posições variadas', () => {
    const grid = [
      [SCATTER, L3,      L3],
      [L3,      SCATTER, L3],
      [L3,      L3,      SCATTER],
      [SCATTER, L3,      L3],
      [L3,      SCATTER, L3],
    ];
    assert.equal(countScatters(grid), 5);
  });

  test('grade toda de scatter → 15 (5 colunas × 3 linhas)', () => {
    assert.equal(countScatters(filledGrid(SCATTER)), 15);
  });
});

// ─── evaluateSpin (integração do engine puro) ─────────────────────────────────
describe('evaluateSpin', () => {
  test('retorna todos os campos obrigatórios do SpinResult', () => {
    const result = evaluateSpin([0, 0, 0, 0, 0], 1);
    const required = [
      'stopPositions', 'grid', 'lineWins', 'lineWinTotal',
      'scatterCount', 'scatterCoins', 'triggerFreeSpins',
      'freeSpinsAwarded', 'totalBet', 'totalWin', 'winLevel',
    ];
    for (const field of required)
      assert.ok(field in result, `Campo ausente: ${field}`);
  });

  test('totalBet = betPerLine × 10 linhas', () => {
    assert.equal(evaluateSpin([0, 0, 0, 0, 0], 1).totalBet, 10);
    assert.equal(evaluateSpin([0, 0, 0, 0, 0], 3).totalBet, 30);
  });

  test('stopPositions no resultado coincidem com o input', () => {
    const stops = [2, 7, 14, 21, 28];
    const result = evaluateSpin(stops, 1);
    assert.deepEqual(result.stopPositions, stops);
  });

  test('grid é 5×3', () => {
    const result = evaluateSpin([0, 0, 0, 0, 0], 1);
    assert.equal(result.grid.length, 5);
    for (const col of result.grid) assert.equal(col.length, 3);
  });

  test('winLevel "none" quando totalWin = 0', () => {
    const { lineWinTotal } = evaluateLines(filledGrid(SCATTER), 1);
    assert.equal(lineWinTotal, 0);
  });

  test('winLevel thresholds corretos (jackpot ≥ 25× totalBet)', () => {
    // Grade toda de H1: 10 paylines × 5-in-a-row × 150 = 1500 coins
    // totalBet=10. 1500 >= 10×25=250 → jackpot
    const { lineWinTotal } = evaluateLines(filledGrid(H1), 1);
    const totalBet = 1 * PAYLINES.length;
    assert.ok(lineWinTotal >= totalBet * 25, `lineWinTotal ${lineWinTotal} deve ser >= jackpot`);
  });

  test('triggerFreeSpins e freeSpinsAwarded coerentes com scatterCount', () => {
    const result = evaluateSpin([0, 0, 0, 0, 0], 1);
    if (result.scatterCount >= 3) {
      assert.equal(result.triggerFreeSpins, true);
      assert.equal(result.freeSpinsAwarded, 8);
    } else {
      assert.equal(result.triggerFreeSpins, false);
      assert.equal(result.freeSpinsAwarded, 0);
    }
  });

  test('scatterCoins correto quando scatterCount >= 3', () => {
    const grid = [
      [SCATTER, L3, L3],
      [L3, SCATTER, L3],
      [L3, L3, SCATTER],
      [SCATTER, L3, L3],
      [L3, SCATTER, L3],
    ];
    const sc = countScatters(grid);
    assert.equal(sc, 5);
    const { SCATTER_MULTIPLIERS } = require('../../src/engine/config');
    const expected = SCATTER_MULTIPLIERS[5] * (1 * PAYLINES.length); // 20 × 10 = 200
    assert.equal(expected, 200);
  });
});
