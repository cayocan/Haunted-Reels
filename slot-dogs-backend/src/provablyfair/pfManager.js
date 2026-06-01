const { v4: uuidv4 } = require('uuid');
const rng = require('../rng/rng');

const sessions = new Map();

function createSession() {
  const sessionId = uuidv4();
  const serverSeed = rng.generateServerSeed();
  const serverSeedHash = rng.hashServerSeed(serverSeed);

  const session = {
    sessionId,
    coins: 500,
    serverSeed,
    serverSeedHash,
    clientSeed: null,
    nonce: 0,
    revealedSeeds: [],
    freeSpinsRemaining: 0,
    betPerLine: 1,
  };

  sessions.set(sessionId, session);

  return {
    sessionId,
    serverSeedHash,
    coins: session.coins,
    betPerLine: session.betPerLine,
  };
}

function setClientSeed(sessionId, clientSeed) {
  const session = sessions.get(sessionId);
  if (!session) return { ok: false, error: 'session_not_found' };
  session.clientSeed = String(clientSeed);
  return { ok: true };
}

function getSession(sessionId) {
  const s = sessions.get(sessionId);
  if (!s) return null;
  return {
    sessionId: s.sessionId,
    coins: s.coins,
    betPerLine: s.betPerLine,
    serverSeedHash: s.serverSeedHash,
    clientSeed: s.clientSeed,
    nonce: s.nonce,
    freeSpinsRemaining: s.freeSpinsRemaining,
  };
}

function rotate(sessionId) {
  const s = sessions.get(sessionId);
  if (!s) return { ok: false, error: 'session_not_found' };
  const revealed = {
    serverSeed: s.serverSeed,
    serverSeedHash: s.serverSeedHash,
    clientSeed: s.clientSeed,
    nonceRange: [0, s.nonce],
  };
  s.revealedSeeds.push(revealed);

  // generate new server seed
  s.serverSeed = rng.generateServerSeed();
  s.serverSeedHash = rng.hashServerSeed(s.serverSeed);

  return { ok: true, revealed, newServerSeedHash: s.serverSeedHash };
}

function getSpinFloats(sessionId) {
  const s = sessions.get(sessionId);
  if (!s) return { ok: false, error: 'session_not_found' };
  if (!s.clientSeed) return { ok: false, error: 'client_seed_missing' };

  const usedNonce = s.nonce;
  const floats = rng.deriveFloatsFromSeed(s.serverSeed, s.clientSeed, usedNonce);
  const stops = floats.map((f) => Math.floor(f * 30));

  // increment nonce after deriving
  s.nonce += 1;

  return { ok: true, stops, nonce: usedNonce };
}

function revealAll() {
  return Array.from(sessions.values()).map((s) => ({ sessionId: s.sessionId, serverSeedHash: s.serverSeedHash }));
}

function verifySpin(serverSeed, clientSeed, nonce, claimedStops) {
  return rng.verifySpin(serverSeed, clientSeed, nonce, claimedStops);
}

module.exports = {
  createSession,
  setClientSeed,
  getSession,
  rotate,
  getSpinFloats,
  verifySpin,
  _internal: { sessions },
};
