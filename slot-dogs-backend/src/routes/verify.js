const pf = require('../provablyfair/pfManager');

module.exports = async function (fastify, opts) {
  fastify.post('/verify', {
    schema: {
      tags: ['Audit'],
      summary: 'Verifica um spin (provably fair)',
      description:
        'Recebe o server seed revelado (via **/session/:id/rotate**), o client seed, o nonce e os ' +
        'stop positions declarados — e recomputa o resultado para confirmar que não houve adulteração.',
      body: {
        type: 'object',
        properties: {
          serverSeed: { type: 'string', description: 'Server seed revelado após rotação' },
          clientSeed: { type: 'string', description: 'Client seed usado na sessão' },
          nonce:      { type: 'integer', description: 'Nonce do spin a auditar' },
          stops:      { type: 'array', items: { type: 'integer' }, description: 'Stop positions declarados [0-29] × 5' },
        },
      },
      response: {
        200: {
          type: 'object',
          properties: {
            valid:            { type: 'boolean', description: 'true se os stops conferem com o seed/nonce' },
            recomputedStops:  { type: 'array', items: { type: 'integer' }, description: 'Stops recalculados pelo servidor' },
          },
        },
        400: { type: 'object', properties: { error: { type: 'string' } } },
      },
    },
  }, async (req, reply) => {
    const { serverSeed, clientSeed, nonce, stops } = req.body || {};
    if (!serverSeed || !clientSeed || typeof nonce !== 'number' || !Array.isArray(stops)) {
      return reply.code(400).send({ error: 'serverSeed, clientSeed, nonce, stops required' });
    }

    const res = pf.verifySpin(serverSeed, clientSeed, nonce, stops);
    return reply.send(res);
  });
};
