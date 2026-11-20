const CITY_BOARDS = ["drycreek", "deadwell", "ravenrock"];
// ScoreSystem awards roundNumber * 100 / max(0.01, factTime), over rounds 1–5.
const CITY_SCORE = { better: "higher", min: 0, max: 150000 };
const BOARDS = {
  sure: { better: "higher", min: 1, max: 500 },
  fastest: { better: "lower", min: 0, max: 120000 },
  steady: { better: "lower", min: 1, max: 120000 },
  drycreek: CITY_SCORE,
  deadwell: CITY_SCORE,
  ravenrock: CITY_SCORE,
  richest: { better: "higher" },
};
const YANDEX_TLDS = new Set([
  "az", "by", "co.il", "com", "com.am", "com.ge", "com.tr", "ee", "fr", "kz",
  "lt", "lv", "md", "ru", "tj", "tm", "uz",
]);

const ID_RE = /^[A-Za-z0-9-]{8,64}$/;

export default {
  async fetch(request, env) {
    const origin = request.headers.get("Origin") || "";
    const cors = corsHeaders(origin);

    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: cors });
    }

    const url = new URL(request.url);
    const parts = url.pathname.split("/").filter(Boolean);
    const board = parts[1];

    if (parts[0] !== "boards" || !Object.hasOwn(BOARDS, board)) {
      return json({ ok: false, error: "not found" }, 404, cors);
    }

    try {
      if (request.method === "GET" && parts.length === 2) {
        const playerId = url.searchParams.get("playerId") || "";
        const page = await loadBoard(env.DB, board, playerId);
        return json({ ok: true, ...page }, 200, cors);
      }

      if (request.method === "POST" && parts.length === 3 && parts[2] === "score") {
        if (board === "richest") {
          return json({ ok: false, error: "read only" }, 405, cors);
        }
        const body = await request.json().catch(() => null);
        const result = await submit(env.DB, board, body);
        return json({ ok: true, ...result }, 200, cors);
      }

      return json({ ok: false, error: "not found" }, 404, cors);
    } catch (e) {
      const bad = e && e.message === "bad request";
      return json({ ok: false, error: bad ? "bad request" : "error" }, bad ? 400 : 500, cors);
    }
  },
};

function json(data, status, cors) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "content-type": "application/json; charset=utf-8", ...cors },
  });
}

function isAllowedOrigin(origin) {
  let url;
  try {
    url = new URL(origin);
  } catch {
    return false;
  }
  if (["http:", "https:"].includes(url.protocol) && ["localhost", "127.0.0.1"].includes(url.hostname)) {
    return true;
  }
  if (url.protocol !== "https:") return false;
  const host = url.hostname;
  const yandexHost = /^(?:(?:m\.)?games?\.)?yandex\.(.+)$/.exec(host);
  return (yandexHost && YANDEX_TLDS.has(yandexHost[1]))
    || host === "games.s3.yandex.net" || host.endsWith(".games.s3.yandex.net")
    || host === "crazygames.com" || host.endsWith(".crazygames.com")
    || host === "gamemonetize.com" || host.endsWith(".gamemonetize.com")
    || host === "gamemonetize.co" || host.endsWith(".gamemonetize.co");
}

function corsHeaders(origin) {
  const headers = {
    "access-control-allow-methods": "GET, POST, OPTIONS",
    "access-control-allow-headers": "Content-Type",
    "access-control-max-age": "86400",
    vary: "Origin",
  };
  if (isAllowedOrigin(origin)) headers["access-control-allow-origin"] = origin;
  return headers;
}

function sanitizeName(raw) {
  const name = String(raw || "").replace(/[\u0000-\u001F|]/g, " ").replace(/\s+/g, " ").trim();
  if (name.length < 2 || name.length > 16) return "";
  return name;
}

function orderBy(board) {
  return BOARDS[board].better === "higher"
    ? "score DESC, updated_at ASC, player_id ASC"
    : "score ASC, updated_at ASC, player_id ASC";
}

async function submit(db, board, body) {
  const spec = BOARDS[board];
  const playerId = String((body && body.playerId) || "").trim();
  const name = sanitizeName(body && body.name);
  const score = body?.score;

  if (!ID_RE.test(playerId) || !name || !Number.isInteger(score) || score < spec.min || score > spec.max) {
    throw new Error("bad request");
  }

  const betterExpr = spec.better === "higher" ? "excluded.score > scores.score" : "excluded.score < scores.score";

  const changed = await db.prepare(`
    INSERT INTO scores (board, player_id, name, score, updated_at)
    VALUES (?1, ?2, ?3, ?4, ?5)
    ON CONFLICT(board, player_id) DO UPDATE SET
      name = excluded.name,
      score = excluded.score,
      updated_at = excluded.updated_at
    WHERE ${betterExpr}
    RETURNING score
  `).bind(board, playerId, name, score, Date.now()).first();

  const rank = await rankOf(db, board, playerId);
  const row = await db.prepare(
    "SELECT score FROM scores WHERE board = ?1 AND player_id = ?2"
  ).bind(board, playerId).first();

  return { accepted: changed !== null, best: row.score, rank };
}

function boardRows(board) {
  if (board !== "richest") {
    return "SELECT player_id, name, score, updated_at FROM scores WHERE board = ?1";
  }

  const cities = CITY_BOARDS.map(id => `'${id}'`).join(", ");
  return `
    SELECT player_id, name, score, updated_at FROM (
      SELECT player_id, name, updated_at,
             SUM(score) OVER (PARTITION BY player_id) AS score,
             ROW_NUMBER() OVER (
               PARTITION BY player_id ORDER BY updated_at DESC, board ASC
             ) AS latest
      FROM scores WHERE board IN (${cities}) AND ?1 = 'richest'
    )
    WHERE latest = 1
  `;
}

async function rankOf(db, board, playerId) {
  const row = await db.prepare(`
    WITH ranked AS (
      SELECT player_id, ROW_NUMBER() OVER (ORDER BY ${orderBy(board)}) AS rank
      FROM (${boardRows(board)})
    )
    SELECT rank FROM ranked WHERE player_id = ?2
  `).bind(board, playerId).first();
  return row ? row.rank : 0;
}

async function loadBoard(db, board, playerId) {
  const safeId = ID_RE.test(playerId) ? playerId : "";
  const listed = await db.prepare(`
    WITH ranked AS (
      SELECT player_id, name, score,
             ROW_NUMBER() OVER (ORDER BY ${orderBy(board)}) AS rank
      FROM (${boardRows(board)})
    ),
    me AS (
      SELECT rank FROM ranked WHERE player_id = ?2
    )
    SELECT player_id AS playerId, name, score, rank FROM ranked
    WHERE rank <= 3
       OR rank IN (
         SELECT rank - 1 FROM me
         UNION SELECT rank FROM me
         UNION SELECT rank + 1 FROM me
       )
    ORDER BY rank
  `).bind(board, safeId).all();

  return {
    userRank: await rankOf(db, board, safeId),
    entries: listed.results || [],
  };
}
