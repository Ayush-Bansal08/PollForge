const express = require('express');
const path = require('path');
const { Pool } = require('pg');
const http = require('http');
const { Server } = require('socket.io');

const app = express();
const server = http.createServer(app);
const io = new Server(server);

const options = (process.env.VOTING_OPTIONS ||
  'Python,Java,JavaScript,C++,Go,Rust,C#,C').split(',');

const pool = new Pool({
  host: process.env.DB_HOST || 'localhost',
  user: process.env.DB_USER || 'postgres',
  password: process.env.DB_PASSWORD || 'postgres',
  database: process.env.DB_NAME || 'postgres',
});

app.use(express.static(path.join(__dirname, 'public')));

app.get('/options', (req, res) => {
  res.json({ options });
});

async function getCurrentCounts() {
  const result = await pool.query('SELECT vote, COUNT(*) AS count FROM votes GROUP BY vote');

  // Start every known option at 0, so an option with zero votes still shows up
  const counts = {};
  options.forEach(opt => { counts[opt] = 0; });

  result.rows.forEach(row => {
    counts[row.vote] = parseInt(row.count, 10);
  });

  return counts;
}

async function connectWithRetry() {
  while (true) {
    try {
      await pool.query('SELECT 1');
      console.log('Connected to Postgres.');
      break;
    } catch (err) {
      console.log('Postgres not ready yet, retrying in 1s...');
      await new Promise(r => setTimeout(r, 1000));
    }
  }
}

io.on('connection', async (socket) => {
  console.log('A client connected:', socket.id);
  socket.emit('scores', await getCurrentCounts());
});



const PORT = 4000;

connectWithRetry().then(() => {
  server.listen(PORT, () => {
    console.log(`Result app listening on port ${PORT}`);

    setInterval(async () => {
      io.emit('scores', await getCurrentCounts());
    }, 1000);
  });
});