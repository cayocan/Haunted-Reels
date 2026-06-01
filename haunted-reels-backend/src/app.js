'use strict';

const Fastify = require('fastify');
const cors = require('@fastify/cors');
const config = require('./engine/config');
const { version } = require('../package.json');

/**
 * Constrói e configura a instância Fastify.
 * Separado de server.js para permitir testes via app.inject().
 *
 * @param {import('fastify').FastifyServerOptions} opts
 */
function buildApp(opts = {}) {
  const app = Fastify(opts);

  // Health check simples — mantido aqui para deploys/monitoramento
  app.get('/health', async (request, reply) => {
    return { status: 'ok' };
  });

  // Swagger deve ser registrado ANTES das rotas para capturar os schemas
  app.register(require('@fastify/swagger'), {
    openapi: {
      openapi: '3.0.3',
      info: {
        title: 'Haunted Reels API',
        version,
        description:
          'API do jogo de slot machine Haunted Reels. Utiliza RNG criptográfico (HMAC-SHA256) ' +
          'e sistema provably fair para garantir auditabilidade de todos os resultados.',
      },
      tags: [
        { name: 'Session', description: 'Criação e gerenciamento de sessão + provably fair' },
        { name: 'Game',    description: 'Mecânica do jogo — realizar spin' },
        { name: 'Audit',   description: 'Verificação independente de resultados' },
        { name: 'Logs',    description: 'Streaming de eventos em tempo real (SSE)' },
      ],
    },
  });

  app.register(require('@fastify/swagger-ui'), {
    routePrefix: '/docs',
    uiConfig: {
      docExpansion: 'list',
      deepLinking: true,
      tryItOutEnabled: true,
    },
    staticCSP: true,
    transformSpecificationClone: true,
  });

  app.register(cors, { origin: process.env.CORS_ORIGIN || '*' });
  app.register(require('./routes/session'));
  app.register(require('./routes/spin'));
  app.register(require('./routes/verify'));
  app.register(require('./routes/logs'));

  // Valida reel strips na inicialização
  for (let i = 0; i < config.REELS.length; i++) {
    if (config.REELS[i].length !== 30) {
      throw new Error(`Reel ${i} tem ${config.REELS[i].length} símbolos; esperado 30`);
    }
  }

  return app;
}

module.exports = { buildApp };
