const crypto = require('crypto');

const SYMBOL_IDS = {
  H1:      0, // Witch    (Spine skin: witch)
  H2:      1, // Pumpkin  (Spine skin: pumpkin)
  H3:      2, // Skull    (Spine skin: skull)
  L1:      3, // Bat
  L2:      4, // Spider
  L3:      5, // Potion
  WILD:    6, // Ghost Wild
  SCATTER: 7, // Cauldron
};

const SYMBOLS = [
  'Witch',
  'Pumpkin',
  'Skull',
  'Bat',
  'Spider',
  'Potion',
  'Wild Ghost',
  'Cauldron (SCATTER)',
];

// Multiplicadores aplicados sobre betPerLine (aposta total ÷ 10)
const PAYTABLE = {
  [SYMBOL_IDS.H1]: [5,   25,  150],
  [SYMBOL_IDS.H2]: [4,   15,  100],
  [SYMBOL_IDS.H3]: [3,   10,   60],
  [SYMBOL_IDS.L1]: [2,    8,   30],
  [SYMBOL_IDS.L2]: [1.5,  6,   20],
  [SYMBOL_IDS.L3]: [1,    4,   15],
};

const SCATTER_MULTIPLIERS = {
  3: 2,
  4: 5,
  5: 20,
};

// Weights por símbolo em cada reel. Ordem: [H1, H2, H3, L1, L2, L3, Wild, Scatter]
// Reels 0 e 4 (bordas) sem Wild — soma = 30 por reel
const REEL_WEIGHTS = [
  [4, 4, 4, 5, 5, 6, 0, 2],  // reel 1 — sem wild
  [3, 3, 4, 5, 5, 6, 2, 2],  // reel 2
  [3, 3, 4, 5, 5, 6, 2, 2],  // reel 3 — central
  [3, 3, 4, 5, 5, 6, 2, 2],  // reel 4
  [4, 4, 4, 5, 5, 6, 0, 2],  // reel 5 — sem wild
];

function expandAndShuffle(weights) {
  const arr = [];
  for (let sym = 0; sym < weights.length; sym++) {
    const count = weights[sym];
    for (let i = 0; i < count; i++) arr.push(sym);
  }

  // Fisher-Yates shuffle usando crypto
  for (let i = arr.length - 1; i > 0; i--) {
    const j = crypto.randomBytes(4).readUInt32BE(0) % (i + 1);
    const tmp = arr[i];
    arr[i] = arr[j];
    arr[j] = tmp;
  }

  return arr;
}

const REELS = REEL_WEIGHTS.map(expandAndShuffle);

// 10 paylines fixas, sempre ativas. Linha 0=Topo, 1=Meio, 2=Base
const PAYLINES = [
  { id: 1,  name: 'Linha do meio',  path: [1, 1, 1, 1, 1] },
  { id: 2,  name: 'Linha de cima',  path: [0, 0, 0, 0, 0] },
  { id: 3,  name: 'Linha de baixo', path: [2, 2, 2, 2, 2] },
  { id: 4,  name: 'V invertido',    path: [0, 1, 2, 1, 0] },
  { id: 5,  name: 'V normal',       path: [2, 1, 0, 1, 2] },
  { id: 6,  name: 'Diagonal ↘',    path: [0, 0, 1, 2, 2] },
  { id: 7,  name: 'Diagonal ↗',    path: [2, 2, 1, 0, 0] },
  { id: 8,  name: 'Escada ↘',      path: [0, 1, 1, 2, 2] },
  { id: 9,  name: 'Escada ↗',      path: [2, 1, 1, 0, 0] },
  { id: 10, name: 'Onda suave ↘',  path: [0, 1, 1, 1, 2] },
];

module.exports = {
  SYMBOL_IDS,
  SYMBOLS,
  PAYTABLE,
  SCATTER_MULTIPLIERS,
  REEL_WEIGHTS,
  REELS,
  PAYLINES,
  WILD_ID:    SYMBOL_IDS.WILD,
  SCATTER_ID: SYMBOL_IDS.SCATTER,
};
