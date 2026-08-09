package database

import (
	"database/sql"
	"fmt"
	"log"
	"time"

	_ "modernc.org/sqlite"
)

var DB *sql.DB

func InitDB(dbPath string) (*sql.DB, error) {
	db, err := sql.Open("sqlite", dbPath)
	if err != nil {
		return nil, fmt.Errorf("failed to open sqlite database: %w", err)
	}

	// Optimize connection pool for high concurrency
	db.SetMaxOpenConns(25)
	db.SetMaxIdleConns(5)
	db.SetConnMaxLifetime(5 * time.Minute)

	// Enable WAL mode for high concurrency
	if _, err := db.Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;"); err != nil {
		log.Printf("[DB] Warning: failed to set WAL mode: %v", err)
	}

	if err := createTables(db); err != nil {
		return nil, fmt.Errorf("failed to create tables: %w", err)
	}

	DB = db
	log.Printf("[DB] Database initialized successfully at %s", dbPath)
	return db, nil
}

func createTables(db *sql.DB) error {
	schema := `
	-- Users table
	CREATE TABLE IF NOT EXISTS users (
		id TEXT PRIMARY KEY,
		email TEXT UNIQUE NOT NULL,
		password_hash TEXT NOT NULL,
		role TEXT NOT NULL DEFAULT 'user',
		created_at DATETIME DEFAULT CURRENT_TIMESTAMP
	);

	-- Hardware Device registry for Anti-Abuse (1 trial per physical device)
	CREATE TABLE IF NOT EXISTS devices (
		hwid_hash TEXT PRIMARY KEY,
		first_user_id TEXT NOT NULL,
		first_claimed_at DATETIME DEFAULT CURRENT_TIMESTAMP,
		trial_claimed INTEGER DEFAULT 1,
		is_banned INTEGER DEFAULT 0,
		ban_reason TEXT DEFAULT '',
		FOREIGN KEY(first_user_id) REFERENCES users(id)
	);

	-- Subscriptions table
	CREATE TABLE IF NOT EXISTS subscriptions (
		id TEXT PRIMARY KEY,
		user_id TEXT NOT NULL,
		hwid_hash TEXT NOT NULL,
		plan_type TEXT NOT NULL,
		status TEXT NOT NULL,
		starts_at DATETIME NOT NULL,
		expires_at DATETIME NOT NULL,
		created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
		FOREIGN KEY(user_id) REFERENCES users(id),
		FOREIGN KEY(hwid_hash) REFERENCES devices(hwid_hash)
	);

	-- Dynamic Relay Servers inventory
	CREATE TABLE IF NOT EXISTS relay_servers (
		id TEXT PRIMARY KEY,
		region_code TEXT NOT NULL,
		display_name TEXT NOT NULL,
		host TEXT NOT NULL,
		port INTEGER NOT NULL DEFAULT 9001,
		is_active INTEGER DEFAULT 1,
		priority INTEGER DEFAULT 1,
		created_at DATETIME DEFAULT CURRENT_TIMESTAMP
	);

	-- Indexes for high performance
	CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
	CREATE INDEX IF NOT EXISTS idx_subscriptions_user_id ON subscriptions(user_id);
	CREATE INDEX IF NOT EXISTS idx_subscriptions_status ON subscriptions(status);
	CREATE INDEX IF NOT EXISTS idx_relay_servers_active ON relay_servers(is_active);
	`

	_, err := db.Exec(schema)
	return err
}
