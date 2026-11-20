CREATE TABLE IF NOT EXISTS scores (
    board TEXT NOT NULL,
    player_id TEXT NOT NULL,
    name TEXT NOT NULL,
    score INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    PRIMARY KEY (board, player_id)
);
