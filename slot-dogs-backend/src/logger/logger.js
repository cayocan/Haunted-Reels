const pino = require('pino');

const logger = pino({
  level: process.env.LOG_LEVEL || 'info',
  transport: process.env.NODE_ENV !== 'production' ? { target: 'pino-pretty' } : undefined,
});

const sseClients = new Set();

function registerSSE(reply) {
  const res = reply.raw;
  res.writeHead(200, {
    'Content-Type': 'text/event-stream',
    'Cache-Control': 'no-cache',
    Connection: 'keep-alive',
  });
  res.write('\n');
  sseClients.add(res);

  res.on('close', () => {
    sseClients.delete(res);
  });
}

function broadcast(logObject) {
  const data = `data: ${JSON.stringify(logObject)}\n\n`;
  for (const res of Array.from(sseClients)) {
    try {
      res.write(data);
    } catch (err) {
      sseClients.delete(res);
    }
  }
}

function info(obj) {
  logger.info(obj);
  try {
    broadcast(obj);
  } catch (e) {
    // ignore
  }
}

module.exports = {
  logger,
  info,
  registerSSE,
  broadcast,
};
