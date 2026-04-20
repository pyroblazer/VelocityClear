-- =============================================================================
-- seed.sql - Reset and populate the platform databases with test data
--
-- Run via the db-seed Docker Compose service:
--   docker-compose run --rm db-seed
--
-- Or manually against a running SQL Server:
--   sqlcmd -S localhost,1433 -U sa -P "YourStrong!Passw0rd" -C -i database/seed.sql
--
-- USER IDs here must stay in sync with the static list in
-- backend/src/FinancialPlatform.ApiGateway/Controllers/AuthController.cs
-- =============================================================================

USE FinancialPlatform_Transactions;
GO

-- Clear existing data (transactions first — no FK constraint but keeps intent clear)
DELETE FROM Transactions;
DELETE FROM Users;
GO

-- =============================================================================
-- USERS
-- Role int mapping: Guest=0, User=1, Admin=2, Auditor=3
-- PasswordHash stores BCrypt hashes (work factor 12). Passwords match the
-- static seed list in AuthController.cs.
-- =============================================================================
INSERT INTO Users (Id, Username, PasswordHash, Role, CreatedAt) VALUES
('a0000000-0000-0000-0000-000000000001', 'admin',    '$2a$12$/lQtv3vrRF.QqWmY1OEsFOtA7GhhDQEo19hrSfk.vfOQdjI0m.HP6',   2, '2026-01-01T00:00:00Z'),
('a0000000-0000-0000-0000-000000000002', 'trader1',  '$2a$12$3BAiQTMvl39DdCAYLPyhTOlW7EvlLOi6DQG.vt88GXl80q7kCK9nG',  1, '2026-01-01T00:00:00Z'),
('a0000000-0000-0000-0000-000000000003', 'auditor1', '$2a$12$DrG9BY1nNKlf3aAOmTBUle2LCymMHolvhutRiWXYzspxyY9EHm8jG',  3, '2026-01-01T00:00:00Z'),
('a0000000-0000-0000-0000-000000000004', 'testuser', '$2a$12$KMyixRvyieKj/z283wQfLOJg.GfSzNGSR4FxOys.XljRhQYIN2Owy',  1, '2026-01-01T00:00:00Z');
GO

-- =============================================================================
-- TRANSACTIONS
-- Status int mapping: Pending=0, Approved=1, Rejected=2, HighRisk=3,
--                     Processing=4, Completed=5, Failed=6
--
-- Risk scoring (for reference — applied live by RiskService, not stored here):
--   +30 if amount > $5 000
--   +20 additional if > $10 000
--   +25 if > 5 txns/min per user
--   +15 if between 22:00–06:00 UTC
--   HIGH >= 80, MEDIUM >= 50, LOW < 50
--
-- Payment auth rules (first match wins):
--   Reject if amount > $50 000
--   Reject if risk >= 80
--   Reject if amount > $5 000 AND risk >= 50
--   Otherwise approve
-- =============================================================================
INSERT INTO Transactions (Id, UserId, Amount, Currency, Timestamp, Status, Description, Counterparty) VALUES

-- admin's transactions
('txn-0001', 'a0000000-0000-0000-0000-000000000001',   500.00, 'USD', '2026-04-10T08:15:00Z', 5, 'Office supplies',           'Staples Corp'),
('txn-0002', 'a0000000-0000-0000-0000-000000000001',  1200.00, 'USD', '2026-04-11T09:30:00Z', 5, 'Software license renewal',  'JetBrains'),
('txn-0003', 'a0000000-0000-0000-0000-000000000001', 75000.00, 'USD', '2026-04-12T14:00:00Z', 2, 'Equipment purchase',        'TechCo Ltd'),
('txn-0004', 'a0000000-0000-0000-0000-000000000001',  3500.00, 'EUR', '2026-04-13T10:45:00Z', 5, 'Conference registration',   'DevConf GmbH'),
('txn-0005', 'a0000000-0000-0000-0000-000000000001', 15000.00, 'USD', '2026-04-14T23:10:00Z', 3, 'Night-time wire transfer',  'Acme Finance'),

-- trader1's transactions (mix of low, medium, and high amounts)
('txn-0006', 'a0000000-0000-0000-0000-000000000002',   250.50, 'USD', '2026-04-08T07:00:00Z', 5, 'Trading fee',               'NYSE Gateway'),
('txn-0007', 'a0000000-0000-0000-0000-000000000002',  8500.00, 'USD', '2026-04-09T11:20:00Z', 1, 'Equity purchase',           'Fidelity Investments'),
('txn-0008', 'a0000000-0000-0000-0000-000000000002', 12000.00, 'USD', '2026-04-10T13:05:00Z', 3, 'Options contract',          'CBOE Exchange'),
('txn-0009', 'a0000000-0000-0000-0000-000000000002',   999.99, 'GBP', '2026-04-11T15:30:00Z', 5, 'FX conversion',             'Barclays FX Desk'),
('txn-0010', 'a0000000-0000-0000-0000-000000000002',  4750.00, 'USD', '2026-04-12T08:45:00Z', 5, 'Dividend reinvestment',     'Vanguard Fund'),
('txn-0011', 'a0000000-0000-0000-0000-000000000002', 55000.00, 'USD', '2026-04-15T16:00:00Z', 2, 'Large block trade',         'Goldman Sachs'),
('txn-0012', 'a0000000-0000-0000-0000-000000000002',  2100.00, 'EUR', '2026-04-16T09:00:00Z', 5, 'ETF purchase',              'Deutsche Bank'),
('txn-0013', 'a0000000-0000-0000-0000-000000000002',   150.00, 'USD', '2026-04-17T12:00:00Z', 5, 'Commission rebate',         'Interactive Brokers'),

-- auditor1's transactions (read access — should only view, not create; these represent
-- audit testing transactions added by admin on their behalf)
('txn-0014', 'a0000000-0000-0000-0000-000000000003',   100.00, 'USD', '2026-04-01T09:00:00Z', 5, 'Audit test — low amount',   'Internal Testing'),
('txn-0015', 'a0000000-0000-0000-0000-000000000003',  6000.00, 'USD', '2026-04-01T09:05:00Z', 1, 'Audit test — medium',       'Internal Testing'),
('txn-0016', 'a0000000-0000-0000-0000-000000000003', 85000.00, 'USD', '2026-04-01T09:10:00Z', 2, 'Audit test — over limit',   'Internal Testing'),

-- testuser's transactions (basic user — good variety for UI testing)
('txn-0017', 'a0000000-0000-0000-0000-000000000004',   49.99, 'USD', '2026-04-18T10:00:00Z', 5, 'Monthly subscription',      'Netflix'),
('txn-0018', 'a0000000-0000-0000-0000-000000000004',  1500.00, 'USD', '2026-04-18T11:00:00Z', 5, 'Rent payment',              'City Rentals LLC'),
('txn-0019', 'a0000000-0000-0000-0000-000000000004',   320.75, 'EUR', '2026-04-19T08:30:00Z', 5, 'Online shopping',           'Amazon EU'),
('txn-0020', 'a0000000-0000-0000-0000-000000000004',  5500.00, 'USD', '2026-04-20T14:00:00Z', 3, 'Home repair payment',       'BuildRight Contractors'),
('txn-0021', 'a0000000-0000-0000-0000-000000000004',   200.00, 'USD', '2026-04-21T07:00:00Z', 0, 'Transfer in progress',      'Personal Savings'),
('txn-0022', 'a0000000-0000-0000-0000-000000000004', 11000.00, 'USD', '2026-04-21T08:00:00Z', 6, 'Wire — processing error',   'Overseas Bank');
GO

PRINT 'Seed complete: 4 users, 22 transactions inserted into FinancialPlatform_Transactions.';
GO
