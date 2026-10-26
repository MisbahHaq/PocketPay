const sqlite3 = require('sqlite3');
const { open } = require('sqlite');

async function setupDb() {
    const db = await open({
        filename: './paypocket.db',
        driver: sqlite3.Database
    });

    await db.exec(`
        CREATE TABLE IF NOT EXISTS users (
            id TEXT PRIMARY KEY,
            email TEXT UNIQUE NOT NULL,
            password_hash TEXT NOT NULL,
            role TEXT NOT NULL,
            created_at_utc TEXT NOT NULL,
            is_locked_out INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS wallets (
            id TEXT PRIMARY KEY,
            user_id TEXT NOT NULL,
            currency TEXT NOT NULL,
            balance_minor_units INTEGER DEFAULT 0,
            FOREIGN KEY(user_id) REFERENCES users(id)
        );

        CREATE TABLE IF NOT EXISTS journal_entries (
            id TEXT PRIMARY KEY,
            occurred_at_utc TEXT NOT NULL,
            description TEXT,
            correlation_id TEXT
        );

        CREATE TABLE IF NOT EXISTS journal_items (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            journal_entry_id TEXT NOT NULL,
            account_code TEXT NOT NULL,
            debit_minor_units INTEGER DEFAULT 0,
            credit_minor_units INTEGER DEFAULT 0,
            FOREIGN KEY(journal_entry_id) REFERENCES journal_entries(id)
        );
    `);

    // Seed initial admin user if empty
    const userCount = await db.get('SELECT count(*) as count FROM users');
    if (userCount.count === 0) {
        await db.run(
            'INSERT INTO users (id, email, password_hash, role, created_at_utc) VALUES (?, ?, ?, ?, ?)',
            ['admin-1', 'admin@paypocket.local', 'hashed_password_123', 'Admin', new Date().toISOString()]
        );
        await db.run(
            'INSERT INTO wallets (id, user_id, currency, balance_minor_units) VALUES (?, ?, ?, ?)',
            ['wallet-1', 'admin-1', 'PKR', 0]
        );
    }

    return db;
}

module.exports = { setupDb };
