const crypto = require('crypto');

function randomFloat() {
  const buf = crypto.randomBytes(4);
  return buf.readUInt32BE(0) / 0x100000000;
}

function randomFloats(n) {
  const buf = crypto.randomBytes(4 * n);
  const res = [];
  for (let i = 0; i < n; i++) {
    res.push(buf.readUInt32BE(i * 4) / 0x100000000);
  }
  return res;
}

function deriveFloatsFromSeed(serverSeed, clientSeed, nonce) {
  const floats = [];
  for (let reelIndex = 0; reelIndex < 5; reelIndex++) {
    const hmac = crypto.createHmac('sha256', Buffer.from(serverSeed, 'hex'));
    hmac.update(`${clientSeed}:${nonce}:${reelIndex}`);
    const digest = hmac.digest();
    floats.push(digest.readUInt32BE(0) / 0x100000000);
  }
  return floats;
}

function generateServerSeed() {
  return crypto.randomBytes(32).toString('hex');
}

function hashServerSeed(seed) {
  return crypto.createHash('sha256').update(seed).digest('hex');
}

function verifySpin(serverSeed, clientSeed, nonce, claimedStops) {
  const floats = deriveFloatsFromSeed(serverSeed, clientSeed, nonce);
  const stops = floats.map((f) => Math.floor(f * 30));
  const valid = stops.every((v, i) => v === claimedStops[i]);
  return { valid, recomputedStops: stops };
}

module.exports = {
  randomFloat,
  randomFloats,
  deriveFloatsFromSeed,
  generateServerSeed,
  hashServerSeed,
  verifySpin,
};
