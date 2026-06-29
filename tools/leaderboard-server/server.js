// Mini backend REST para testear el leaderboard del juego — SIN dependencias (http nativo).
//
// Cumple el contrato que espera LeaderboardService:
//   POST /scores            body = JSON ScoreDto {name,time,topSpeed,track,timestamp}  -> guarda
//   GET  /scores?limit=N    -> array JSON de ScoreDto ordenado por time asc (top-N)
//
// Persiste a scores.json (al lado de este archivo). Al arrancar, si no hay datos,
// siembra N entradas random para tener algo en la tabla.
//
// Uso:
//   node server.js            (puerto 3000 por defecto)
//   PORT=8080 node server.js
//
// En Unity: LeaderboardService._baseUrl = http://localhost:3000/scores

const http = require("http");
const fs   = require("fs");
const path = require("path");

const PORT      = process.env.PORT || 3000;
const DATA_FILE = path.join(__dirname, "scores.json");
const SEED_N    = 8; // cuántos scores random sembrar si la DB está vacía

// ── Persistencia simple en archivo ─────────────────────────────────────────
function load() {
  try { return JSON.parse(fs.readFileSync(DATA_FILE, "utf8")); }
  catch { return null; }
}
function save(scores) {
  fs.writeFileSync(DATA_FILE, JSON.stringify(scores, null, 2));
}

// ── Datos random de seed ────────────────────────────────────────────────────
const NAMES  = ["Speedy", "Nitro", "Blaze", "Turbo", "Ghost", "Vortex", "Comet", "Rusty", "Dash", "Zephyr"];
const TRACKS = ["Track01"];

function randomScore() {
  const name     = NAMES[Math.floor(Math.random() * NAMES.length)];
  const time     = +(30 + Math.random() * 90).toFixed(3);   // 30s..120s
  const position = 1 + Math.floor(Math.random() * 8);        // 1..8
  const track    = TRACKS[Math.floor(Math.random() * TRACKS.length)];
  return { name, time, position, track, timestamp: new Date().toISOString() };
}

function seed(n) {
  return Array.from({ length: n }, randomScore).sort((a, b) => a.time - b.time);
}

// ── Estado ────────────────────────────────────────────────────────────────
let scores = load();
if (!scores || scores.length === 0) {
  scores = seed(SEED_N);
  save(scores);
  console.log(`[mock] DB vacía -> sembrados ${scores.length} scores random.`);
} else {
  console.log(`[mock] Cargados ${scores.length} scores de ${DATA_FILE}`);
}

// ── Helpers HTTP ─────────────────────────────────────────────────────────────
function sendJson(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, { "Content-Type": "application/json" });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve) => {
    let data = "";
    req.on("data", (chunk) => (data += chunk));
    req.on("end", () => resolve(data));
  });
}

// ── Server ────────────────────────────────────────────────────────────────
const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);

  if (url.pathname !== "/scores") {
    sendJson(res, 404, { error: "Not found. Usá /scores" });
    return;
  }

  if (req.method === "GET") {
    const limit = parseInt(url.searchParams.get("limit"), 10);
    const sorted = [...scores].sort((a, b) => a.time - b.time);
    const out = Number.isFinite(limit) && limit > 0 ? sorted.slice(0, limit) : sorted;
    console.log(`[mock] GET /scores?limit=${limit || "-"} -> ${out.length} filas`);
    sendJson(res, 200, out);
    return;
  }

  if (req.method === "POST") {
    const raw = await readBody(req);
    try {
      const dto = JSON.parse(raw);
      scores.push(dto);
      save(scores);
      console.log(`[mock] POST /scores <- ${dto.name} ${dto.time}s (total: ${scores.length})`);
      sendJson(res, 201, dto);
    } catch (e) {
      console.log(`[mock] POST body inválido: ${raw}`);
      sendJson(res, 400, { error: "JSON inválido" });
    }
    return;
  }

  sendJson(res, 405, { error: "Método no permitido" });
});

server.listen(PORT, () => {
  console.log(`[mock] Leaderboard REST escuchando en http://localhost:${PORT}/scores`);
  console.log(`[mock] Seteá ese URL en LeaderboardService._baseUrl`);
});
