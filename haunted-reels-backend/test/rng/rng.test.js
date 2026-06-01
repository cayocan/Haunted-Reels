'use strict';

const { describe, test } = require('node:test');
const assert = require('node:assert/strict');
const rng = require('../../src/rng/rng');

describe('randomFloat', () => {
  test('retorna valor no intervalo [0, 1)', () => {
    for (let i = 0; i < 100; i++) {
      const v = rng.randomFloat();
      assert.ok(v >= 0 && v < 1, `Valor fora do range: ${v}`);
    }
  });

  test('produz valores distintos em chamadas sucessivas (probabilidade de colisão desprezível)', () => {
    const a = rng.randomFloat();
    const b = rng.randomFloat();
    // Com 32 bits de entropia, probabilidade de colisão = 1/2^32 ≈ 0
    assert.notEqual(a, b);
  });
});

describe('randomFloats(n)', () => {
  test('retorna array com n valores', () => {
    for (const n of [1, 5, 10, 100]) {
      assert.equal(rng.randomFloats(n).length, n);
    }
  });

  test('todos os valores estão em [0, 1)', () => {
    const values = rng.randomFloats(50);
    for (const v of values) assert.ok(v >= 0 && v < 1, `Valor fora do range: ${v}`);
  });
});

describe('generateServerSeed', () => {
  test('retorna string hexadecimal de 64 caracteres (32 bytes)', () => {
    const seed = rng.generateServerSeed();
    assert.equal(typeof seed, 'string');
    assert.equal(seed.length, 64);
    assert.match(seed, /^[0-9a-f]{64}$/);
  });

  test('seeds gerados são distintos', () => {
    assert.notEqual(rng.generateServerSeed(), rng.generateServerSeed());
  });
});

describe('hashServerSeed', () => {
  test('retorna SHA-256 hex de 64 caracteres', () => {
    const seed = rng.generateServerSeed();
    const hash = rng.hashServerSeed(seed);
    assert.equal(hash.length, 64);
    assert.match(hash, /^[0-9a-f]{64}$/);
  });

  test('função é determinística (mesma entrada → mesma saída)', () => {
    const seed = rng.generateServerSeed();
    assert.equal(rng.hashServerSeed(seed), rng.hashServerSeed(seed));
  });

  test('seeds diferentes produzem hashes diferentes', () => {
    const s1 = rng.generateServerSeed();
    const s2 = rng.generateServerSeed();
    assert.notEqual(rng.hashServerSeed(s1), rng.hashServerSeed(s2));
  });
});

describe('deriveFloatsFromSeed', () => {
  const serverSeed = 'a'.repeat(64); // hex válido de 32 bytes (simulado como ASCII em buffer)
  const clientSeed = 'meu-client-seed-seguro';
  const nonce = 0;

  test('retorna exatamente 5 floats', () => {
    const floats = rng.deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
    assert.equal(floats.length, 5);
  });

  test('todos os floats estão em [0, 1)', () => {
    const floats = rng.deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
    for (const f of floats) assert.ok(f >= 0 && f < 1, `Float fora do range: ${f}`);
  });

  test('é determinístico (mesmos inputs → mesmos outputs)', () => {
    const f1 = rng.deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
    const f2 = rng.deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
    assert.deepEqual(f1, f2);
  });

  test('nonce diferente produz floats diferentes', () => {
    const f0 = rng.deriveFloatsFromSeed(serverSeed, clientSeed, 0);
    const f1 = rng.deriveFloatsFromSeed(serverSeed, clientSeed, 1);
    // É extremamente improvável que todos os 5 floats sejam iguais com nonce diferente
    assert.notDeepEqual(f0, f1);
  });

  test('clientSeed diferente produz floats diferentes', () => {
    const f1 = rng.deriveFloatsFromSeed(serverSeed, 'client-a', nonce);
    const f2 = rng.deriveFloatsFromSeed(serverSeed, 'client-b', nonce);
    assert.notDeepEqual(f1, f2);
  });
});

describe('verifySpin', () => {
  test('retorna valid=true para stops corretos derivados do seed', () => {
    const serverSeed = rng.generateServerSeed();
    const clientSeed = 'meu-seed-cliente-99';
    const nonce = 3;
    const floats = rng.deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
    const stops = floats.map(f => Math.floor(f * 30));
    const result = rng.verifySpin(serverSeed, clientSeed, nonce, stops);
    assert.equal(result.valid, true);
    assert.deepEqual(result.recomputedStops, stops);
  });

  test('retorna valid=false se stops forem adulterados', () => {
    const serverSeed = rng.generateServerSeed();
    const clientSeed = 'meu-seed-cliente-99';
    const nonce = 0;
    const floats = rng.deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
    const stops = floats.map(f => Math.floor(f * 30));
    const adulterado = stops.map((s, i) => (i === 0 ? (s + 1) % 30 : s));
    const result = rng.verifySpin(serverSeed, clientSeed, nonce, adulterado);
    assert.equal(result.valid, false);
  });

  test('retorna valid=false com nonce errado', () => {
    const serverSeed = rng.generateServerSeed();
    const clientSeed = 'meu-seed-teste';
    const floats = rng.deriveFloatsFromSeed(serverSeed, clientSeed, 0);
    const stops = floats.map(f => Math.floor(f * 30));
    // Verificar com nonce=1 mas stops derivados do nonce=0
    const result = rng.verifySpin(serverSeed, clientSeed, 1, stops);
    assert.equal(result.valid, false);
  });
});
