const log = require('../logger/logger');

module.exports = async function (fastify, opts) {
  fastify.get('/logs/stream', {
    schema: {
      tags: ['Logs'],
      summary: 'Stream de eventos em tempo real (SSE)',
      description:
        'Abre uma conexão Server-Sent Events. Cada spin emite um evento JSON com ' +
        '`sessionId`, `nonce`, `totalBet`, `totalWin`, `winLevel`, `stopPositions`. ' +
        'Use EventSource no cliente ou `curl -N http://localhost:3000/logs/stream`.',
      response: {
        200: { description: 'Conexão SSE aberta (text/event-stream)', type: 'string' },
      },
    },
  }, async (req, reply) => {
    // register SSE client
    log.registerSSE(reply);
    // do not end the reply so connection stays open
    return reply.raw;
  });
};
