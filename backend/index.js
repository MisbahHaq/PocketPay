const express = require('express');
const cors = require('cors');
require('dotenv').config();
const { setupDb } = require('./db');
const jwt = require('jsonwebtoken');
const bcrypt = require('bcryptjs');
const { v4: uuidv4 } = require('uuid');
const { z } = require('zod');

const app = express();
const PORT = process.env.PORT || 3001;
const JWT_SECRET = process.env.JWT_SECRET || 'super-secret-paypocket-key-12345';

app.use(cors());
app.use(express.json());

let db;

// Validation Schemas
const FundWalletSchema = z.object({
    amountMinorUnits: z.number().positive().int(),
    currency: z.enum(['PKR', 'USD']),
});

// Middleware: Authentication & Authorization
async function authenticate(req, res, next) {
    const authHeader = req.headers.authorization;
    if (!authHeader || !authHeader.startsWith('Bearer ')) {
        return res.status(401).json({ message: 'Authentication required' });
    }

    const token = authHeader.split(' ')[1];
    try {
        const decoded = jwt.verify(token, JWT_SECRET);
        req.user = decoded;
        next();
    } catch (err) {
        return res.status(401).json({ message: 'Invalid or expired token' });
    }
}

async function authorizeAdmin(req, res, next) {
    if (!req.user || req.user.role !== 'Admin') {
        return res.status(403).json({ message: 'Administrative privileges required' });
    }
    next();
}

async function startServer() {
    try {
        db = await setupDb();
        console.log('✅ Database connected');

        app.post('/api/auth/login', async (req, res) => {
            const { email, password } = req.body;
            const user = await db.get('SELECT * FROM users WHERE email = ?', [email]);
            
            if (!user) {
                return res.status(401).json({ message: 'Invalid credentials' });
            }

            if (password !== 'password123') {
                return res.status(401).json({ message: 'Invalid credentials' });
            }

            const token = jwt.sign(
                { sub: user.id, email: user.email, role: user.role },
                JWT_SECRET,
                { expiresIn: '1h' }
            );

            res.json({
                accessToken: token,
                refreshToken: 'demo-refresh-token'
            });
        });

        // Protected Admin Routes
        app.get('/api/admin/users', authenticate, authorizeAdmin, async (req, res) => {
            const page = parseInt(req.query.Page) || 1;
            const pageSize = parseInt(req.query.PageSize) || 10;
            const offset = (page - 1) * pageSize;

            const items = await db.all('SELECT id, email, role, created_at_utc as createdAtUtc, is_locked_out as isLockedOut FROM users LIMIT ? OFFSET ?', [pageSize, offset]);
            const totalCount = await db.get('SELECT count(*) as count FROM users');

            res.json({
                items,
                totalCount: totalCount.count,
                page,
                pageSize
            });
        });

        app.get('/api/admin/ledger/trial-balance', authenticate, authorizeAdmin, async (req, res) => {
            const totals = await db.all(`
                SELECT 
                    account_code as currency, 
                    SUM(debit_minor_units) as totalDebits, 
                    SUM(credit_minor_units) as totalCredits 
                FROM journal_items 
                GROUP BY account_code
            `);
            
            if (totals.length === 0) {
                return res.json({ currencies: [] });
            }
            res.json({ currencies: totals });
        });

        app.post('/api/admin/wallets/:id/fund', authenticate, authorizeAdmin, async (req, res) => {
            const { id } = req.params;
            const idempotencyKey = req.headers['idempotency-key'];

            if (!idempotencyKey) {
                return res.status(400).json({ message: 'Idempotency-Key header is required' });
            }

            // Check if this request was already processed
            const existingEntry = await db.get(
                'SELECT id FROM journal_entries WHERE correlation_id = ?', 
                [idempotencyKey]
            );

            if (existingEntry) {
                return res.status(200).json({
                    message: 'Request already processed',
                    journalEntryId: existingEntry.id
                });
            }

            // Input Validation
            const validation = FundWalletSchema.safeParse(req.body);
            if (!validation.success) {
                return res.status(400).json({ 
                    message: 'Validation failed', 
                    errors: validation.error.issues 
                });
            }
            const { amountMinorUnits, currency } = validation.data;
            const journalId = uuidv4();

            try {
                await db.run('BEGIN TRANSACTION');

                const wallet = await db.get('SELECT * FROM wallets WHERE id = ?', [id]);
                if (!wallet) {
                    const defaultUser = await db.get('SELECT id FROM users LIMIT 1');
                    await db.run(
                        'INSERT INTO wallets (id, user_id, currency, balance_minor_units) VALUES (?, ?, ?, 0)',
                        [id, defaultUser.id, currency]
                    );
                }

                await db.run(
                    'INSERT INTO journal_entries (id, occurred_at_utc, description, correlation_id) VALUES (?, ?, ?, ?)',
                    [journalId, new Date().toISOString(), `Funding wallet ${id}`, idempotencyKey]
                );

                await db.run(
                    'INSERT INTO journal_items (journal_entry_id, account_code, credit_minor_units) VALUES (?, ?, ?)',
                    [journalId, `WALLET:${id}`, amountMinorUnits]
                );

                await db.run(
                    'INSERT INTO journal_items (journal_entry_id, account_code, debit_minor_units) VALUES (?, ?, ?)',
                    [journalId, 'SYSTEM_FUNDING', amountMinorUnits]
                );

                await db.run(
                    'UPDATE wallets SET balance_minor_units = balance_minor_units + ? WHERE id = ?',
                    [amountMinorUnits, id]
                );

                await db.run('COMMIT');
                res.status(201).json({
                    journalEntryId: journalId,
                    walletId: id,
                    amountMinorUnits,
                    currency
                });
            } catch (err) {
                await db.run('ROLLBACK');
                res.status(500).json({ message: 'Transaction failed', error: err.message });
            }
        });

        app.get('/api/admin/audit', authenticate, authorizeAdmin, async (req, res) => {
            const items = await db.all(`
                SELECT je.id, je.occurred_at_utc as occurredAtUtc, je.description 
                FROM journal_entries je 
                ORDER BY je.occurred_at_utc DESC
            `);
            res.json({
                items,
                totalCount: items.length
            });
        });

        app.get('/health/live', (req, res) => res.json({ status: 'Healthy' }));

        app.listen(PORT, () => {
            console.log(`🚀 PayPocket Backend running on http://localhost:${PORT}`);
        });
    } catch (err) {
        console.error('Failed to start server:', err);
        process.exit(1);
    }
}

startServer();

