import { test, expect, type Page } from '@playwright/test';

const BASE = 'http://localhost:4173';

// ---------------------------------------------------------------------------
// Shared API mocks — covers both backend proxies (Gateway :5000 + HSM :5005)
// ---------------------------------------------------------------------------

async function authenticate(page: Page) {
  await page.goto(BASE);
  await page.evaluate(() => {
    localStorage.setItem('vc_auth', JSON.stringify({
      token: 'e2e-test-token',
      role: 'Admin',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    }));
  });
}

async function setupMocks(page: Page) {
  // Transaction API (proxied to API Gateway)
  await page.route('**/api/transactions', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'txn-001', userId: 'user1', amount: 250.00, currency: 'USD', status: 'completed', timestamp: new Date().toISOString() },
          { id: 'txn-002', userId: 'user2', amount: 7500.00, currency: 'EUR', status: 'pending', timestamp: new Date().toISOString() },
        ]),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'txn-new', userId: 'testuser', amount: 100, currency: 'USD', status: 'pending', timestamp: new Date().toISOString() }),
      });
    }
  });

  // SSE stream — return empty stream so the browser doesn't hang
  await page.route('**/api/transactions/stream', async (route) => {
    await route.fulfill({ status: 200, contentType: 'text/event-stream', body: '' });
  });

  // HSM keys
  await page.route('**/api/hsm/keys', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(['default-zpk']) });
  });

  await page.route('**/api/hsm/keys/generate', async (route) => {
    const body = JSON.parse(route.request().postData() ?? '{}');
    if (body.keyId === 'default-zpk') {
      await route.fulfill({ status: 409, contentType: 'application/json', body: JSON.stringify({ error: "Key 'default-zpk' already exists." }) });
    } else {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ keyId: body.keyId, keyType: body.keyType, keyCheckValue: 'DEF456', encryptedUnderLmk: 'AABBCCDD' }) });
    }
  });

  await page.route('**/api/hsm/pin/encrypt', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ encryptedPinBlock: 'AABBCCDDEEFF0011', keyCheckValue: 'ABC123', format: 'ISO9564-0' }) });
  });

  await page.route('**/api/hsm/pin/decrypt', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ pin: '1234', format: 'ISO9564-0' }) });
  });

  await page.route('**/api/hsm/pin/verify', async (route) => {
    const body = JSON.parse(route.request().postData() ?? '{}');
    const verified = body.expectedPin === '1234';
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ verified, message: verified ? 'PIN correct' : 'PIN mismatch' }) });
  });

  await page.route('**/api/iso8583/parse', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ mti: '0100', fields: { '2': '4111111111111111', '4': '000000001000', '49': 'USD' }, mtiDescription: 'Authorization Request' }) });
  });

  await page.route('**/api/iso8583/build', async (route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isoMessage: '0100F0000000000000000000000000000000000012341111111111111111000000001000USD', length: 80 }) });
  });

  await page.route('**/api/iso8583/authorize', async (route) => {
    const body = JSON.parse(route.request().postData() ?? '{}');
    const approved = !body.pan?.startsWith('4999');
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(approved
        ? { approved: true, responseCode: '00', authorizationId: '654321', message: 'Approved' }
        : { approved: false, responseCode: '05', authorizationId: '', message: 'Declined - Do not honour' }),
    });
  });

  // Gateway health
  await page.route('**/api/gateway/health', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'Healthy',
        services: {
          'API Gateway': { name: 'API Gateway', status: 'Healthy', url: 'http://localhost:5000' },
          'Transaction Service': { name: 'Transaction Service', status: 'Healthy', url: 'http://localhost:5001' },
          'Risk Service': { name: 'Risk Service', status: 'Healthy', url: 'http://localhost:5002' },
          'Payment Service': { name: 'Payment Service', status: 'Healthy', url: 'http://localhost:5003' },
          'Compliance Service': { name: 'Compliance Service', status: 'Healthy', url: 'http://localhost:5004' },
          'PinEncryptionService': { name: 'PinEncryptionService', status: 'Healthy', url: 'http://localhost:5005' },
        },
      }),
    });
  });

  // Gateway status
  await page.route('**/api/gateway/status', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ activeSseConnections: 3, eventBusBackend: 'Redis' }),
    });
  });

  // Audit stats
  await page.route('**/api/audit/stats', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ totalEvents: 142, eventsByType: { TransactionCreated: 80, RiskEvaluated: 62 } }),
    });
  });

  // Audit list
  await page.route('**/api/audit?page=**', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 'audit-1', eventType: 'TransactionCreated', payload: '{"amount":1000,"userId":"user1","transactionId":"TXN-2024-0847"}', hash: 'A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6', previousHash: null, createdAt: '2024-08-15T10:00:00Z' },
        { id: 'audit-2', eventType: 'RiskEvaluated', payload: '{"riskScore":75,"transactionId":"TXN-2024-0848"}', hash: 'D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2', previousHash: 'A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6', createdAt: '2024-08-15T10:01:00Z' },
        { id: 'audit-3', eventType: 'PaymentAuthorized', payload: '{"amount":1000,"transactionId":"TXN-2024-0847"}', hash: 'B5C6D7E8F9A0B1C2D3E4F5A6B7C8D9E0F1A2B3C4D5E6F7A8B9C0D1E2', previousHash: 'D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2', createdAt: '2024-08-15T10:02:00Z' },
      ]),
    });
  });

  // Audit verify
  await page.route('**/api/audit/verify', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ isValid: true, totalLogs: 3, verifiedLogs: 3 }),
    });
  });

  // Auth login (for any auth checks)
  await page.route('**/api/auth/login', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ token: 'e2e-test-token', role: 'Admin', expiresAt: new Date(Date.now() + 3600000).toISOString() }),
    });
  });
}

// ---------------------------------------------------------------------------
// Global layout
// ---------------------------------------------------------------------------

test.describe('Superapp — layout', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto(BASE);
  });

  test('renders the VelocityClear header', async ({ page }) => {
    await expect(page.getByText('VelocityClear')).toBeVisible();
  });

  test('shows sidebar with all 5 sections', async ({ page }) => {
    await expect(page.getByTestId('nav-transactions')).toBeVisible();
    await expect(page.getByTestId('nav-admin')).toBeVisible();
    await expect(page.getByTestId('nav-risk')).toBeVisible();
    await expect(page.getByTestId('nav-audit')).toBeVisible();
    await expect(page.getByTestId('nav-cards')).toBeVisible();
  });

  test('redirects / to /transactions', async ({ page }) => {
    await expect(page).toHaveURL(/\/transactions/);
  });

  test('shows Live indicator in header', async ({ page }) => {
    await expect(page.getByTestId('app-header').getByText('Live')).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// Transactions page
// ---------------------------------------------------------------------------

test.describe('Superapp — Transactions page', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto(`${BASE}/transactions`);
  });

  test('renders KPI cards', async ({ page }) => {
    // KPI card labels use CSS uppercase but DOM text is mixed-case
    await expect(page.locator('text=Total Transactions').first()).toBeVisible();
    await expect(page.locator('text=Total Volume').first()).toBeVisible();
    await expect(page.locator('text=Average Amount').first()).toBeVisible();
    await expect(page.locator('text=Pending').first()).toBeVisible();
  });

  test('renders the transaction form', async ({ page }) => {
    await expect(page.getByText('New Transaction')).toBeVisible();
    await expect(page.getByTestId('txn-user-id')).toBeVisible();
    await expect(page.getByTestId('txn-amount')).toBeVisible();
  });

  test('submit button is disabled when fields are empty', async ({ page }) => {
    await expect(page.getByTestId('txn-submit')).toBeDisabled();
  });

  test('submit button enables after filling required fields', async ({ page }) => {
    await page.getByTestId('txn-user-id').fill('user-42');
    await page.getByTestId('txn-amount').fill('500');
    await expect(page.getByTestId('txn-submit')).toBeEnabled();
  });

  test('submits a transaction and shows success banner', async ({ page }) => {
    await page.getByTestId('txn-user-id').fill('user-42');
    await page.getByTestId('txn-amount').fill('500');
    await page.getByTestId('txn-submit').click();
    await expect(page.getByText('Transfer submitted successfully')).toBeVisible();
  });

  test('shows the transaction table with loaded data', async ({ page }) => {
    await expect(page.getByText('user1')).toBeVisible({ timeout: 10_000 });
  });

  test('renders Live Feed panel', async ({ page }) => {
    await expect(page.getByText('Live Feed')).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// Admin page
// ---------------------------------------------------------------------------

test.describe('Superapp — Admin page', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto(`${BASE}/admin`);
  });

  test('renders Admin Control Panel heading', async ({ page }) => {
    await expect(page.getByText('Admin Control Panel')).toBeVisible();
  });

  test('shows all 6 service cards', async ({ page }) => {
    // Locate port numbers which are unique to each service card
    await expect(page.getByText(':5000')).toBeVisible();
    await expect(page.getByText(':5001')).toBeVisible();
    await expect(page.getByText(':5002')).toBeVisible();
    await expect(page.getByText(':5003')).toBeVisible();
    await expect(page.getByText(':5004')).toBeVisible();
    await expect(page.getByText(':5005')).toBeVisible();
  });

  test('run health check button triggers refetch', async ({ page }) => {
    await page.getByTestId('health-check-btn').click();
    await expect(page.getByTestId('health-check-btn')).toContainText('Run Health Check');
  });

  test('shows metrics panel when View Metrics is clicked', async ({ page }) => {
    await page.getByTestId('metrics-btn').click();
    await expect(page.getByText('Total Events')).toBeVisible();
    await expect(page.getByText('142')).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// Risk page
// ---------------------------------------------------------------------------

test.describe('Superapp — Risk page', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto(`${BASE}/risk`);
  });

  test('renders Risk Monitoring heading', async ({ page }) => {
    await expect(page.getByText('Risk Monitoring')).toBeVisible();
  });

  test('shows current risk score gauge', async ({ page }) => {
    await expect(page.getByText('Current Risk Score')).toBeVisible();
  });

  test('shows risk distribution chart', async ({ page }) => {
    await expect(page.getByText('Risk Distribution')).toBeVisible();
  });

  test('shows risk trend chart', async ({ page }) => {
    await expect(page.getByText('Risk Trend (24h)')).toBeVisible();
  });

  test('shows recent risk events table', async ({ page }) => {
    await expect(page.getByText('Recent Risk Events')).toBeVisible();
    await expect(page.getByText('TXN-2024-0847')).toBeVisible();
  });

  test('shows HIGH risk alert banner', async ({ page }) => {
    await expect(page.getByText(/HIGH risk transaction/)).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// Audit page
// ---------------------------------------------------------------------------

test.describe('Superapp — Audit page', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto(`${BASE}/audit`);
  });

  test('renders Audit Trail heading', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Audit Trail' })).toBeVisible();
  });

  test('shows total events count', async ({ page }) => {
    await expect(page.getByText('Total Events')).toBeVisible();
  });

  test('shows Chain Integrity badge', async ({ page }) => {
    await page.getByRole('button', { name: 'Verify Chain' }).click();
    await expect(page.getByText('Chain Verified')).toBeVisible();
  });

  test('renders hash chain timeline entries', async ({ page }) => {
    await expect(page.getByText('TransactionCreated').first()).toBeVisible();
    await expect(page.getByText('PaymentAuthorized').first()).toBeVisible();
  });

  test('filter by TransactionCreated hides RiskEvaluated timeline entries', async ({ page }) => {
    // RiskEvaluated should be visible in the timeline before filtering
    const timeline = page.locator('section:has-text("Hash Chain Timeline")');
    await expect(timeline.getByText('RiskEvaluated').first()).toBeVisible();
    await page.getByRole('button', { name: 'TransactionCreated' }).click();
    // The RiskEvaluated entry should disappear from the timeline (not from filter buttons)
    await expect(timeline.getByText('RiskEvaluated').first()).not.toBeVisible();
  });

  test('search filters the timeline', async ({ page }) => {
    const timeline = page.locator('section:has-text("Hash Chain Timeline")');
    await page.getByTestId('audit-search').fill('TXN-2024-0848');
    await expect(timeline.getByText('TransactionCreated').first()).not.toBeVisible();
    await expect(timeline.getByText('RiskEvaluated').first()).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// Card Operations page
// ---------------------------------------------------------------------------

test.describe('Superapp — Card Operations page', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto(`${BASE}/cards`);
  });

  test('renders HSM Key Management section', async ({ page }) => {
    await expect(page.getByText('HSM Key Management')).toBeVisible();
  });

  test('renders PIN Operations section', async ({ page }) => {
    await expect(page.getByText('PIN Operations')).toBeVisible();
  });

  test('renders ISO 8583 Tools section', async ({ page }) => {
    await expect(page.getByText('ISO 8583 Tools')).toBeVisible();
  });

  test('shows default-zpk in active keys', async ({ page }) => {
    await expect(page.getByText('default-zpk')).toBeVisible();
  });

  test('generate key button is disabled when key ID is empty', async ({ page }) => {
    await expect(page.getByTestId('generate-key-btn')).toBeDisabled();
  });

  test('generates a new key and shows success', async ({ page }) => {
    await page.getByTestId('key-id-input').fill('my-zpk-test');
    await page.getByTestId('generate-key-btn').click();
    await expect(page.getByText(/Generated ZPK key: my-zpk-test/)).toBeVisible();
  });

  test('encrypts a PIN and displays the block', async ({ page }) => {
    await page.getByTestId('encrypt-pin-input').fill('1234');
    await page.getByTestId('encrypt-pan-input').fill('4111111111111111');
    await page.getByTestId('encrypt-btn').click();
    await expect(page.getByText('AABBCCDDEEFF0011')).toBeVisible();
  });

  test('decrypts an encrypted PIN block', async ({ page }) => {
    await page.getByTestId('decrypt-block-input').fill('AABBCCDDEEFF0011');
    await page.getByTestId('decrypt-pan-input').fill('4111111111111111');
    await page.getByTestId('decrypt-btn').click();
    await expect(page.getByText('PIN: 1234')).toBeVisible();
  });

  test('verifies a correct PIN', async ({ page }) => {
    await page.getByTestId('verify-block-input').fill('AABBCCDDEEFF0011');
    await page.getByTestId('verify-pan-input').fill('4111111111111111');
    await page.getByTestId('verify-expected-input').fill('1234');
    await page.getByTestId('verify-btn').click();
    await expect(page.getByText('PIN Verified')).toBeVisible();
  });

  test('verifies a wrong PIN and shows mismatch', async ({ page }) => {
    await page.getByTestId('verify-block-input').fill('AABBCCDDEEFF0011');
    await page.getByTestId('verify-pan-input').fill('4111111111111111');
    await page.getByTestId('verify-expected-input').fill('9999');
    await page.getByTestId('verify-btn').click();
    await expect(page.getByText('PIN Mismatch')).toBeVisible();
  });

  test('parses an ISO 8583 message', async ({ page }) => {
    await page.getByTestId('parse-input').fill('0100F0000000000000000');
    await page.getByTestId('parse-btn').click();
    await expect(page.getByText('MTI: 0100')).toBeVisible();
    await expect(page.getByText('Authorization Request')).toBeVisible();
  });

  test('builds an ISO 8583 message', async ({ page }) => {
    await page.getByTestId('build-btn').click();
    await expect(page.getByText('0100F000000000000000')).toBeVisible();
  });

  test('approves a card authorization with valid PAN', async ({ page }) => {
    await page.getByTestId('auth-pan-input').fill('4111111111111111');
    await page.getByTestId('authorize-btn').click();
    await expect(page.getByText('APPROVED').first()).toBeVisible();
  });

  test('declines a card with deny-listed PAN', async ({ page }) => {
    await page.getByTestId('auth-pan-input').fill('4999111111111111');
    await page.getByTestId('authorize-btn').click();
    await expect(page.getByText('DECLINED').first()).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// Navigation between sections
// ---------------------------------------------------------------------------

test.describe('Superapp — navigation', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto(BASE);
  });

  test('clicking Admin nav shows Admin Control Panel', async ({ page }) => {
    await page.getByTestId('nav-admin').click();
    await expect(page.getByText('Admin Control Panel')).toBeVisible();
    await expect(page).toHaveURL(/\/admin/);
  });

  test('clicking Risk nav shows Risk Monitoring', async ({ page }) => {
    await page.getByTestId('nav-risk').click();
    await expect(page.getByText('Risk Monitoring')).toBeVisible();
    await expect(page).toHaveURL(/\/risk/);
  });

  test('clicking Audit nav shows Audit Trail', async ({ page }) => {
    await page.getByTestId('nav-audit').click();
    await expect(page.getByRole('heading', { name: 'Audit Trail' })).toBeVisible();
    await expect(page).toHaveURL(/\/audit/);
  });

  test('clicking Cards nav shows Card Operations', async ({ page }) => {
    await page.getByTestId('nav-cards').click();
    await expect(page.getByRole('heading', { name: 'Card Operations' })).toBeVisible();
    await expect(page).toHaveURL(/\/cards/);
  });

  test('navigating back to Transactions restores the page', async ({ page }) => {
    await page.getByTestId('nav-admin').click();
    await page.getByTestId('nav-transactions').click();
    await expect(page.getByText('New Transaction')).toBeVisible();
    await expect(page).toHaveURL(/\/transactions/);
  });
});
