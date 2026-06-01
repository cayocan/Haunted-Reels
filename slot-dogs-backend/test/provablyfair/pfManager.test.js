'use strict';

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const pf = require('../../src/provablyfair/pfManager');
const rng = require('../../src/rng/rng');

describe('createSession', () => {
  test('retorna sessionId, serverSeedHash, coins=100, betPerLine=1', () => {
    const s = pf.createSession();
    assert.ok(s.sessionId, 'deve ter sessionId');
    assert.ok(s.serverSeedHash, 'deve ter serverSeedHash');
    assert.equal(s.coins, 500);
    assert.equal(s.betPerLine, 1);
  });

  test('serverSeedHash é SHA-256 hex de 64 chars', () => {
    const { serverSeedHash } = pf.createSession();
    assert.match(serverSeedHash, /^[0-9a-f]{64}$/);
  });

  test('sessionId é UUID v4', () => {
    const { sessionId } = pf.createSession();
    assert.match(sessionId, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });

  test('sessions diferentes têm sessionIds e hashes únicos', () => {
    const a = pf.createSession();
    const b = pf.createSession();
    assert.notEqual(a.sessionId, b.sessionId);
    assert.notEqual(a.serverSeedHash, b.serverSeedHash);
  });

  test('serverSeed NUNCA é exposto na resposta de createSession', () => {
    const result = pf.createSession();
    assert.ok(!('serverSeed' in result), 'serverSeed não deve ser retornado');
  });
});

describe('setClientSeed', () => {
  test('retorna {ok: true} para seed válido', () => {
    const { sessionId } = pf.createSession();
    const result = pf.setClientSeed(sessionId, 'meu-seed-seguro');
    assert.deepEqual(result, { ok: true });
  });

  test('retorna {ok: false} para sessionId inexistente', () => {
    const result = pf.setClientSeed('nao-existe-000', 'qualquer');
    assert.equal(result.ok, false);
  });

  test('clientSeed é persistido na sessão', () => {
    const { sessionId } = pf.createSession();
    pf.setClientSeed(sessionId, 'seed-persistido');
    const sess = pf.getSession(sessionId);
    assert.equal(sess.clientSeed, 'seed-persistido');
  });
});

describe('getSession', () => {
  test('retorna estado da sessão sem expor serverSeed', () => {
    const { sessionId } = pf.createSession();
    const sess = pf.getSession(sessionId);
    assert.ok(!('serverSeed' in sess), 'serverSeed não deve aparecer no getSession');
    assert.equal(sess.coins, 500);
    assert.equal(sess.nonce, 0);
    assert.equal(sess.freeSpinsRemaining, 0);
  });

  test('retorna null para sessionId inexistente', () => {
    assert.equal(pf.getSession('session-invalida-xyz'), null);
  });
});

describe('getSpinFloats', () => {
  test('retorna erro se clientSeed não estiver definido', () => {
    const { sessionId } = pf.createSession();
    // clientSeed não definido → ok=false
    const res = pf.getSpinFloats(sessionId);
    assert.equal(res.ok, false);
    assert.equal(res.error, 'client_seed_missing');
  });

  test('retorna 5 stops em [0, 29] e nonce usado', () => {
    const { sessionId } = pf.createSession();
    pf.setClientSeed(sessionId, 'meu-seed-de-test');
    const res = pf.getSpinFloats(sessionId);
    assert.equal(res.ok, true);
    assert.equal(res.stops.length, 5);
    for (const stop of res.stops)
      assert.ok(stop >= 0 && stop <= 29, `Stop fora do range: ${stop}`);
  });

  test('nonce incrementa a cada chamada', () => {
    const { sessionId } = pf.createSession();
    pf.setClientSeed(sessionId, 'seed-nonce-test');
    const r0 = pf.getSpinFloats(sessionId);
    const r1 = pf.getSpinFloats(sessionId);
    assert.equal(r0.nonce, 0);
    assert.equal(r1.nonce, 1);
    const sess = pf.getSession(sessionId);
    assert.equal(sess.nonce, 2);
  });

  test('stops derivados são reproduzíveis (provably fair)', () => {
    const { sessionId } = pf.createSession();
    const sess = pf._internal.sessions.get(sessionId);
    pf.setClientSeed(sessionId, 'seed-reproducivel');

    const { stops, nonce } = pf.getSpinFloats(sessionId);
    // Verificar que os stops batem com a derivação manual
    const verify = rng.verifySpin(sess.serverSeed, sess.clientSeed, nonce, stops);
    assert.equal(verify.valid, true);
  });
});

describe('rotate', () => {
  test('revela serverSeed atual e gera novo hash', () => {
    const { sessionId } = pf.createSession();
    const sessAntes = pf._internal.sessions.get(sessionId);
    const hashAntes = sessAntes.serverSeedHash;

    const result = pf.rotate(sessionId);
    assert.equal(result.ok, true);
    assert.ok(result.revealed.serverSeed, 'deve revelar serverSeed');
    assert.equal(result.revealed.serverSeedHash, hashAntes);
    assert.ok(result.newServerSeedHash, 'deve retornar novo hash');
    assert.notEqual(result.newServerSeedHash, hashAntes);
  });

  test('serverSeed revelado corresponde ao hash anterior', () => {
    const { sessionId } = pf.createSession();
    const sessAntes = pf._internal.sessions.get(sessionId);
    const seedOriginal = sessAntes.serverSeed;

    const result = pf.rotate(sessionId);
    assert.equal(result.revealed.serverSeed, seedOriginal);
    // Verificar que o hash bate
    const computed = rng.hashServerSeed(result.revealed.serverSeed);
    assert.equal(computed, result.revealed.serverSeedHash);
  });

  test('retorna erro para sessionId inexistente', () => {
    const result = pf.rotate('session-invalida');
    assert.equal(result.ok, false);
  });

  test('nonceRange reflete os nonces usados antes da rotação', () => {
    const { sessionId } = pf.createSession();
    pf.setClientSeed(sessionId, 'seed-rotate-test');
    pf.getSpinFloats(sessionId); // nonce 0
    pf.getSpinFloats(sessionId); // nonce 1

    const result = pf.rotate(sessionId);
    assert.deepEqual(result.revealed.nonceRange, [0, 2]);
  });
});

describe('verifySpin (pfManager wrapper)', () => {
  test('delega corretamente para rng.verifySpin', () => {
    const serverSeed = rng.generateServerSeed();
    const clientSeed = 'seed-verify-test';
    const nonce = 7;
    const floats = rng.deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
    const stops = floats.map(f => Math.floor(f * 30));

    const result = pf.verifySpin(serverSeed, clientSeed, nonce, stops);
    assert.equal(result.valid, true);
  });
});
