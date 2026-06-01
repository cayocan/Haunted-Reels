'use strict';

const { describe, test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const { buildApp } = require('../../src/app');

let app;

before(async () => {
  app = buildApp({ logger: false });
  await app.ready();
});

after(async () => {
  await app.close();
});

// Helper: cria sessão com clientSeed já configurado, pronto para spin
async function createReadySession(clientSeed = 'seed-teste-spin-ok') {
  const sess = (await app.inject({ method: 'POST', url: '/session' })).json();
  await app.inject({
    method: 'POST',
    url: `/session/${sess.sessionId}/seed`,
    headers: { 'content-type': 'application/json' },
    payload: { clientSeed },
  });
  return sess;
}

// ─── Validação de inputs ───────────────────────────────────────────────────────
describe('POST /spin — validação', () => {
  test('retorna 400 quando sessionId está ausente', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: {},
    });
    assert.equal(res.statusCode, 400);
    assert.equal(res.json().error, 'sessionId_required');
  });

  test('retorna 404 para sessionId inexistente', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: 'sessao-invalida-999' },
    });
    assert.equal(res.statusCode, 404);
    assert.equal(res.json().error, 'session_not_found');
  });

  test('retorna 400 se clientSeed não foi definido', async () => {
    const sess = (await app.inject({ method: 'POST', url: '/session' })).json();
    // Não chama /seed — clientSeed ausente
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId },
    });
    assert.equal(res.statusCode, 400);
    assert.equal(res.json().error, 'clientSeed_required');
  });

  test('retorna 400 para betPerLine=0 (inválido)', async () => {
    const sess = await createReadySession();
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId, betPerLine: 0 },
    });
    assert.equal(res.statusCode, 400);
  });

  test('retorna 400 para betPerLine negativo', async () => {
    const sess = await createReadySession();
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId, betPerLine: -1 },
    });
    assert.equal(res.statusCode, 400);
  });

  test('retorna 400 para betPerLine fracionário', async () => {
    const sess = await createReadySession();
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId, betPerLine: 1.5 },
    });
    assert.equal(res.statusCode, 400);
  });
});

// ─── Spin válido ───────────────────────────────────────────────────────────────
describe('POST /spin — resposta válida', () => {
  test('retorna 200 com spin, session e provablyFair', async () => {
    const sess = await createReadySession();
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId, betPerLine: 1 },
    });
    assert.equal(res.statusCode, 200);
    const body = res.json();
    assert.ok(body.spin, 'deve ter campo spin');
    assert.ok(body.session, 'deve ter campo session');
    assert.ok(body.provablyFair, 'deve ter campo provablyFair');
  });

  test('spin contém todos os campos obrigatórios do SpinResult', async () => {
    const sess = await createReadySession();
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId },
    });
    const { spin } = res.json();
    const required = [
      'stopPositions', 'grid', 'lineWins', 'lineWinTotal',
      'scatterCount', 'scatterCoins', 'triggerFreeSpins',
      'freeSpinsAwarded', 'totalBet', 'totalWin', 'winLevel',
    ];
    for (const f of required) assert.ok(f in spin, `Campo ausente no spin: ${f}`);
  });

  test('totalBet = betPerLine × 12', async () => {
    const sess = await createReadySession();
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId, betPerLine: 3 },
    });
    assert.equal(res.json().spin.totalBet, 36);
  });

  test('coins nunca diminuem (ENABLE_COST desativado)', async () => {
    const sess = await createReadySession();
    for (let i = 0; i < 5; i++) {
      const res = await app.inject({
        method: 'POST',
        url: '/spin',
        headers: { 'content-type': 'application/json' },
        payload: { sessionId: sess.sessionId, betPerLine: 1 },
      });
      assert.ok(res.json().session.coins >= 100,
        `Coins não devem diminuir abaixo de 100; foram ${res.json().session.coins}`);
    }
  });

  test('nonce na sessão incrementa após cada spin', async () => {
    const sess = await createReadySession();
    const r1 = (await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId },
    })).json();
    const r2 = (await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId },
    })).json();
    assert.equal(r1.session.nonce, 1);
    assert.equal(r2.session.nonce, 2);
    // nonce do provablyFair é o nonce USADO (= session.nonce - 1)
    assert.equal(r1.provablyFair.nonce, 0);
    assert.equal(r2.provablyFair.nonce, 1);
  });

  test('provablyFair.clientSeed corresponde ao seed configurado', async () => {
    const clientSeed = 'seed-especifico-111';
    const sess = await createReadySession(clientSeed);
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId },
    });
    assert.equal(res.json().provablyFair.clientSeed, clientSeed);
  });

  test('betPerLine ausente usa betPerLine da sessão (padrão 1)', async () => {
    const sess = await createReadySession();
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId }, // betPerLine omitido
    });
    assert.equal(res.json().spin.totalBet, 12); // 1 × 12
  });

  test('freeSpinsRemaining incrementa quando scatter dispara free spins', async () => {
    // Este teste verifica que a lógica de free spins está conectada.
    // Não é possível forçar 3+ scatters deterministicamente, mas verificamos
    // que o campo existe e é coerente com o spin.
    const sess = await createReadySession();
    const res = await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId: sess.sessionId },
    });
    const body = res.json();
    if (body.spin.triggerFreeSpins) {
      assert.ok(body.session.freeSpinsRemaining > 0);
    } else {
      assert.ok(body.session.freeSpinsRemaining >= 0);
    }
  });
});
