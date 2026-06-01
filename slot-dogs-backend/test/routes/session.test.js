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

// ─── POST /session ─────────────────────────────────────────────────────────────
describe('POST /session', () => {
  test('retorna 201 com serverSeedHash e coins=500', async () => {
    const res = await app.inject({ method: 'POST', url: '/session' });
    assert.equal(res.statusCode, 201);
    const body = res.json();
    assert.ok(body.sessionId, 'deve ter sessionId');
    assert.ok(body.serverSeedHash, 'deve ter serverSeedHash');
    assert.equal(body.coins, 500);
    assert.equal(body.betPerLine, 1);
  });

  test('serverSeed NÃO é exposto na resposta', async () => {
    const res = await app.inject({ method: 'POST', url: '/session' });
    const body = res.json();
    assert.ok(!('serverSeed' in body), 'serverSeed não deve ser retornado');
  });
});

// ─── POST /session/:id/seed ────────────────────────────────────────────────────
describe('POST /session/:id/seed', () => {
  test('aceita clientSeed válido e retorna {ok: true}', async () => {
    const sess = (await app.inject({ method: 'POST', url: '/session' })).json();
    const res = await app.inject({
      method: 'POST',
      url: `/session/${sess.sessionId}/seed`,
      headers: { 'content-type': 'application/json' },
      payload: { clientSeed: 'seed-valido-12345' },
    });
    assert.equal(res.statusCode, 200);
    assert.deepEqual(res.json(), { ok: true });
  });

  test('retorna 400 para clientSeed com menos de 8 caracteres', async () => {
    const sess = (await app.inject({ method: 'POST', url: '/session' })).json();
    const res = await app.inject({
      method: 'POST',
      url: `/session/${sess.sessionId}/seed`,
      headers: { 'content-type': 'application/json' },
      payload: { clientSeed: 'curto' },
    });
    assert.equal(res.statusCode, 400);
  });

  test('retorna 400 para clientSeed ausente', async () => {
    const sess = (await app.inject({ method: 'POST', url: '/session' })).json();
    const res = await app.inject({
      method: 'POST',
      url: `/session/${sess.sessionId}/seed`,
      headers: { 'content-type': 'application/json' },
      payload: {},
    });
    assert.equal(res.statusCode, 400);
  });

  test('retorna 404 para sessionId inexistente', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/session/sessao-que-nao-existe/seed',
      headers: { 'content-type': 'application/json' },
      payload: { clientSeed: 'seed-valido-12345' },
    });
    assert.equal(res.statusCode, 404);
  });
});

// ─── GET /session/:id ──────────────────────────────────────────────────────────
describe('GET /session/:id', () => {
  test('retorna 200 com o estado da sessão', async () => {
    const sess = (await app.inject({ method: 'POST', url: '/session' })).json();
    const res = await app.inject({ method: 'GET', url: `/session/${sess.sessionId}` });
    assert.equal(res.statusCode, 200);
    const body = res.json();
    assert.equal(body.coins, 500);
    assert.equal(body.nonce, 0);
    assert.ok(!('serverSeed' in body), 'serverSeed não deve aparecer');
  });

  test('retorna 404 para sessionId inexistente', async () => {
    const res = await app.inject({ method: 'GET', url: '/session/nao-existe-abc' });
    assert.equal(res.statusCode, 404);
  });
});

// ─── POST /session/:id/rotate ──────────────────────────────────────────────────
describe('POST /session/:id/rotate', () => {
  test('revela serverSeed e gera novo hash', async () => {
    const sess = (await app.inject({ method: 'POST', url: '/session' })).json();
    const res = await app.inject({
      method: 'POST',
      url: `/session/${sess.sessionId}/rotate`,
    });
    assert.equal(res.statusCode, 200);
    const body = res.json();
    assert.equal(body.ok, true);
    assert.ok(body.revealed.serverSeed, 'deve expor serverSeed na rotação');
    assert.equal(body.revealed.serverSeedHash, sess.serverSeedHash);
    assert.ok(body.newServerSeedHash, 'deve ter novo hash');
    assert.notEqual(body.newServerSeedHash, sess.serverSeedHash);
  });

  test('retorna 404 para sessionId inexistente', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/session/nao-existe-rotate/rotate',
    });
    assert.equal(res.statusCode, 404);
  });
});
