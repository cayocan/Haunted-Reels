const pf = require('../provablyfair/pfManager');

const sessionStateSchema = {
  type: 'object',
  properties: {
    sessionId:           { type: 'string', format: 'uuid' },
    serverSeedHash:      { type: 'string', description: 'SHA-256 do server seed ativo' },
    coins:               { type: 'integer', example: 100 },
    betPerLine:          { type: 'integer', example: 1 },
    nonce:               { type: 'integer', example: 0 },
    freeSpinsRemaining:  { type: 'integer', example: 0 },
    clientSeed:          { type: 'string',  nullable: true },
  },
};

const errorSchema = {
  type: 'object',
  properties: { error: { type: 'string' } },
};

const idParam = {
  type: 'object',
  properties: { id: { type: 'string', description: 'Session ID (UUID)' } },
};

module.exports = async function (fastify, opts) {
  fastify.post('/session', {
    schema: {
      tags: ['Session'],
      summary: 'Cria uma nova sessão de jogo',
      description:
        'Gera um server seed aleatório e retorna seu hash SHA-256. ' +
        'O seed real só é revelado via **POST /session/:id/rotate** (provably fair).',
      response: {
        201: { description: 'Sessão criada', ...sessionStateSchema },
      },
    },
  }, async (req, reply) => {
    const s = pf.createSession();
    return reply.code(201).send(s);
  });

  fastify.post('/session/:id/seed', {
    schema: {
      tags: ['Session'],
      summary: 'Define o client seed da sessão',
      description:
        'O client seed é combinado via HMAC-SHA256 com o server seed e o nonce ' +
        'para derivar os stop positions de cada reel — garantindo auditabilidade.',
      params: idParam,
      body: {
        type: 'object',
        properties: {
          clientSeed: {
            type: 'string',
            minLength: 8,
            description: 'Seed escolhido pelo jogador (mínimo 8 caracteres)',
          },
        },
      },
      response: {
        200: { type: 'object', properties: { ok: { type: 'boolean' } } },
        400: { type: 'object', properties: { ok: { type: 'boolean' }, error: { type: 'string' } } },
        404: errorSchema,
      },
    },
  }, async (req, reply) => {
    const { id } = req.params;
    const { clientSeed } = req.body || {};
    if (!clientSeed || String(clientSeed).length < 8) {
      return reply.code(400).send({ ok: false, error: 'clientSeed must be at least 8 characters' });
    }
    const res = pf.setClientSeed(id, clientSeed);
    if (!res.ok) return reply.code(404).send(res);
    return reply.send({ ok: true });
  });

  fastify.get('/session/:id', {
    schema: {
      tags: ['Session'],
      summary: 'Consulta o estado atual da sessão',
      description: 'Retorna coins, nonce atual, free spins restantes e hash do server seed. O server seed nunca é exposto.',
      params: idParam,
      response: {
        200: { description: 'Estado da sessão (sem expor o server seed)', ...sessionStateSchema },
        404: errorSchema,
      },
    },
  }, async (req, reply) => {
    const { id } = req.params;
    const s = pf.getSession(id);
    if (!s) return reply.code(404).send({ error: 'session_not_found' });
    return reply.send(s);
  });

  fastify.post('/session/:id/rotate', {
    schema: {
      tags: ['Session'],
      summary: 'Rotaciona o server seed (auditoria)',
      description:
        'Revela o server seed atual (para verificação via **/verify**) e gera um novo par seed/hash. ' +
        'Deve ser chamado antes de iniciar uma nova rodada de auditoria.',
      params: idParam,
      response: {
        200: {
          type: 'object',
          properties: {
            ok: { type: 'boolean' },
            revealed: {
              type: 'object',
              properties: {
                serverSeed:     { type: 'string', description: 'Seed revelado — use em /verify para auditar spins anteriores' },
                serverSeedHash: { type: 'string' },
                nonceRange:     { type: 'array', items: { type: 'integer' }, description: '[nonceInício, nonceFim]' },
              },
            },
            newServerSeedHash: { type: 'string', description: 'Hash do novo seed já ativo' },
          },
        },
        404: errorSchema,
      },
    },
  }, async (req, reply) => {
    const { id } = req.params;
    const res = pf.rotate(id);
    if (!res.ok) return reply.code(404).send(res);
    return reply.send(res);
  });
};
