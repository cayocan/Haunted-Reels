const pf = require('../provablyfair/pfManager');
const engine = require('../engine/spinEngine');
const logger = require('../logger/logger');
const { PAYLINES } = require('../engine/config');

function deductCoins(session, amount) {
  session.coins -= amount;
}

const spinResponseSchema = {
  type: 'object',
  properties: {
    spin: {
      type: 'object',
      properties: {
        stopPositions:    { type: 'array', items: { type: 'integer' }, description: 'Índice (0-29) de parada de cada reel' },
        grid:            { type: 'array', items: { type: 'array', items: { type: 'integer' } }, description: 'Grade 5×3 de symbolIds' },
        lineWins: {
          type: 'array',
          items: {
            type: 'object',
            properties: {
              lineId:     { type: 'integer' },
              symbolId:   { type: 'integer' },
              count:      { type: 'integer' },
              multiplier: { type: 'number' },
              coins:      { type: 'number' },
              cells:      { type: 'array', items: { type: 'array', items: { type: 'integer' } } },
            },
          },
        },
        lineWinTotal:     { type: 'number' },
        scatterCount:     { type: 'integer' },
        scatterPositions: { type: 'array', items: { type: 'array', items: { type: 'integer' } } },
        scatterCoins:     { type: 'number' },
        triggerFreeSpins: { type: 'boolean' },
        freeSpinsAwarded: { type: 'integer' },
        totalBet:         { type: 'integer' },
        totalWin:         { type: 'number' },
        winLevel:         { type: 'string', enum: ['none', 'small', 'big', 'mega', 'jackpot'] },
      },
    },
    session: {
      type: 'object',
      properties: {
        coins:              { type: 'integer' },
        freeSpinsRemaining: { type: 'integer' },
        nonce:              { type: 'integer' },
        serverSeedHash:     { type: 'string' },
      },
    },
    provablyFair: {
      type: 'object',
      properties: {
        serverSeedHash: { type: 'string' },
        clientSeed:     { type: 'string' },
        nonce:          { type: 'integer', description: 'Nonce usado neste spin (para /verify)' },
      },
    },
  },
};

module.exports = async function (fastify, opts) {
  fastify.post('/spin', {
    schema: {
      tags: ['Game'],
      summary: 'Realiza um spin',
      description:
        'Deriva 5 stop positions via HMAC-SHA256(serverSeed, clientSeed, nonce), ' +
        'avalia 10 paylines + scatters e retorna o resultado completo com dados para auditoria.',
      body: {
        type: 'object',
        properties: {
          sessionId:  { type: 'string', description: 'ID da sessão ativa' },
          betPerLine: { type: 'integer', minimum: 1, description: 'Aposta por linha (padrão: betPerLine da sessão)' },
        },
      },
      response: {
        200: { description: 'Resultado do spin', ...spinResponseSchema },
        400: { type: 'object', properties: { error: { type: 'string' } } },
        404: { type: 'object', properties: { error: { type: 'string' } } },
      },
    },
  }, async (req, reply) => {
    const { sessionId, betPerLine } = req.body || {};
    if (!sessionId) return reply.code(400).send({ error: 'sessionId_required' });

    const session = pf._internal.sessions.get(sessionId);
    if (!session) return reply.code(404).send({ error: 'session_not_found' });
    if (!session.clientSeed) return reply.code(400).send({ error: 'clientSeed_required' });

    // Valida betPerLine se fornecido explicitamente
    if (betPerLine !== undefined && betPerLine !== null) {
      if (!Number.isInteger(betPerLine) || betPerLine < 1) {
        return reply.code(400).send({ error: 'betPerLine deve ser inteiro >= 1' });
      }
    }
    // frontend envia aposta total; betPerLine interno = totalBet / nº de paylines
    const totalBet = (betPerLine !== undefined && betPerLine !== null) ? betPerLine : session.betPerLine;
    const bet = totalBet / PAYLINES.length;

    // Decrementa free spins antes de qualquer lógica de custo
    const isFreePin = session.freeSpinsRemaining > 0;
    if (isFreePin) session.freeSpinsRemaining -= 1;

    // Valida e desconta moedas (free spins não têm custo)
    if (!isFreePin) {
      if ((session.coins || 0) < totalBet) {
        return reply.code(400).send({ error: 'insufficient_coins' });
      }
      deductCoins(session, totalBet);
    }

    const floatsRes = pf.getSpinFloats(sessionId);
    if (!floatsRes.ok) return reply.code(400).send(floatsRes);

    const { stops, nonce } = floatsRes;

    const spin = engine.evaluateSpin(stops, bet);

    session.coins = (session.coins || 0) + spin.totalWin;
    // re-trigger permitido também durante free spins (acumula +8 ao saldo restante)
    if (spin.triggerFreeSpins)
      session.freeSpinsRemaining = (session.freeSpinsRemaining || 0) + spin.freeSpinsAwarded;

    // Log via pino + broadcast SSE
    const logObj = {
      level: 'info',
      time: Date.now(),
      msg: 'spin',
      sessionId: session.sessionId,
      nonce,
      totalBet: spin.totalBet,
      totalWin: spin.totalWin,
      winLevel: spin.winLevel,
      scatterCount: spin.scatterCount,
      triggerFreeSpins: spin.triggerFreeSpins,
      stopPositions: spin.stopPositions,
    };
    logger.info(logObj);

    return reply.send({
      spin,
      session: {
        coins: session.coins,
        freeSpinsRemaining: session.freeSpinsRemaining,
        nonce: session.nonce,
        serverSeedHash: session.serverSeedHash,
      },
      provablyFair: {
        serverSeedHash: session.serverSeedHash,
        clientSeed: session.clientSeed,
        nonce,
      },
    });
  });
};
