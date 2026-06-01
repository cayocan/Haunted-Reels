'use strict';

const { describe, test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const { buildApp } = require('../../src/app');
const rng = require('../../src/rng/rng');
const pf = require('../../src/provablyfair/pfManager');

let app;

before(async () => {
  app = buildApp({ logger: false });
  await app.ready();
});

after(async () => {
  await app.close();
});

// Helper: gera dados válidos para verificação
function validVerifyPayload() {
  const serverSeed = rng.generateServerSeed();
  const clientSeed = 'verify-test-seed-ok';
  const nonce = 5;
  const floats = rng.deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
  const stops = floats.map(f => Math.floor(f * 30));
  return { serverSeed, clientSeed, nonce, stops };
}

// ─── POST /verify ──────────────────────────────────────────────────────────────
describe('POST /verify', () => {
  test('retorna {valid: true} para stops corretos', async () => {
    const payload = validVerifyPayload();
    const res = await app.inject({
      method: 'POST',
      url: '/verify',
      headers: { 'content-type': 'application/json' },
      payload,
    });
    assert.equal(res.statusCode, 200);
    const body = res.json();
    assert.equal(body.valid, true);
    assert.deepEqual(body.recomputedStops, payload.stops);
  });

  test('retorna {valid: false} para stops adulterados', async () => {
    const payload = validVerifyPayload();
    const adulterado = payload.stops.map((s, i) => (i === 0 ? (s + 1) % 30 : s));
    const res = await app.inject({
      method: 'POST',
      url: '/verify',
      headers: { 'content-type': 'application/json' },
      payload: { ...payload, stops: adulterado },
    });
    assert.equal(res.statusCode, 200);
    assert.equal(res.json().valid, false);
  });

  test('retorna {valid: false} para nonce errado', async () => {
    const payload = validVerifyPayload();
    const res = await app.inject({
      method: 'POST',
      url: '/verify',
      headers: { 'content-type': 'application/json' },
      payload: { ...payload, nonce: payload.nonce + 1 },
    });
    assert.equal(res.statusCode, 200);
    assert.equal(res.json().valid, false);
  });

  test('retorna 400 quando serverSeed está ausente', async () => {
    const payload = validVerifyPayload();
    const { serverSeed, ...rest } = payload;
    const res = await app.inject({
      method: 'POST',
      url: '/verify',
      headers: { 'content-type': 'application/json' },
      payload: rest,
    });
    assert.equal(res.statusCode, 400);
  });

  test('retorna 400 quando clientSeed está ausente', async () => {
    const payload = validVerifyPayload();
    const { clientSeed, ...rest } = payload;
    const res = await app.inject({
      method: 'POST',
      url: '/verify',
      headers: { 'content-type': 'application/json' },
      payload: rest,
    });
    assert.equal(res.statusCode, 400);
  });

  test('retorna 400 quando nonce não é número', async () => {
    const payload = validVerifyPayload();
    const res = await app.inject({
      method: 'POST',
      url: '/verify',
      headers: { 'content-type': 'application/json' },
      payload: { ...payload, nonce: 'zero' },
    });
    assert.equal(res.statusCode, 400);
  });

  test('retorna 400 quando stops não é array', async () => {
    const payload = validVerifyPayload();
    const res = await app.inject({
      method: 'POST',
      url: '/verify',
      headers: { 'content-type': 'application/json' },
      payload: { ...payload, stops: '0,1,2,3,4' },
    });
    assert.equal(res.statusCode, 400);
  });

  test('integração end-to-end: verify confirma spin real feito via /spin', async () => {
    // Cria sessão + define clientSeed
    const sessionRes = (await app.inject({ method: 'POST', url: '/session' })).json();
    const sessionId = sessionRes.sessionId;
    await app.inject({
      method: 'POST',
      url: `/session/${sessionId}/seed`,
      headers: { 'content-type': 'application/json' },
      payload: { clientSeed: 'seed-e2e-verify-test' },
    });

    // Faz spin
    const spinRes = (await app.inject({
      method: 'POST',
      url: '/spin',
      headers: { 'content-type': 'application/json' },
      payload: { sessionId },
    })).json();

    // Rotaciona seed para obter o serverSeed revelado
    const rotateRes = (await app.inject({
      method: 'POST',
      url: `/session/${sessionId}/rotate`,
    })).json();

    // Verifica o spin com o seed revelado
    const verifyRes = (await app.inject({
      method: 'POST',
      url: '/verify',
      headers: { 'content-type': 'application/json' },
      payload: {
        serverSeed: rotateRes.revealed.serverSeed,
        clientSeed: spinRes.provablyFair.clientSeed,
        nonce: spinRes.provablyFair.nonce,
        stops: spinRes.spin.stopPositions,
      },
    })).json();

    assert.equal(verifyRes.valid, true);
  });
});
