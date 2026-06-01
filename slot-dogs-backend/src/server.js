'use strict';

require('dotenv').config();
const { buildApp } = require('./app');
const { logger } = require('./logger/logger');
const config = require('./engine/config');
const { version } = require('../package.json');

const app = buildApp({ logger });
const port = process.env.PORT ? Number(process.env.PORT) : 3000;

app.listen({ port, host: '0.0.0.0' }, (err, address) => {
  if (err) {
    app.log.error(err);
    process.exit(1);
  }
  app.log.info(`Servidor Haunted Reels rodando em ${address}`);
  app.log.info(`Versão: ${version} | Porta: ${port} | RTP alvo: 96% | Linhas: ${config.PAYLINES.length}`);
});
