const express = require('express');
const dotenv = require('dotenv');
const swaggerUi = require('swagger-ui-express');
const swaggerJsdoc = require('swagger-jsdoc');

dotenv.config();

const app = express();
const port = process.env.PORT || 3000;

// Health check route (must be first, before any other route)
app.get('/health', (req, res) => {
  res.json({ status: 'ok' });
});

const swaggerDefinition = {
  openapi: '3.0.0',
  info: {
    title: 'Haunted Reels API',
    version: '0.1.0',
    description: 'Documentação mínima da API Haunted Reels',
  },
  servers: [
    {
      url: `http://localhost:${port}`,
      description: 'Servidor local',
    },
  ],
};

const options = {
  definition: swaggerDefinition,
  apis: ['./index.js'],
};

const swaggerSpec = swaggerJsdoc(options);

app.use('/docs', swaggerUi.serve, swaggerUi.setup(swaggerSpec));

/** 
 * @openapi
 * /:
 *   get:
 *     summary: Retorna status do servidor
 *     responses:
 *       200:
 *         description: OK
 *         content:
 *           application/json:
 *             schema:
 *               type: object
 *               properties:
 *                 status:
 *                   type: string
 *                 time:
 *                   type: string
 */
app.get('/', (req, res) => {
  res.json({ status: 'ok', time: new Date().toISOString() });
});

app.listen(port, () => {
  console.log(`Backend rodando em http://localhost:${port}`);
  console.log(`Docs em http://localhost:${port}/docs`);
});
