import { test, expect, type Page } from '@playwright/test';

async function authenticate(page: Page) {
  await page.goto('/login');
  await page.evaluate(() => {
    localStorage.setItem('vc_auth', JSON.stringify({
      token: 'e2e-test-token',
      role: 'Admin',
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    }));
  });
}

const mockTransactions = [
  {
    id: 'txn-001',
    userId: 'user_001',
    amount: 250.0,
    currency: 'USD',
    status: 'Completed',
    timestamp: '2024-01-15T14:32:18Z',
    description: 'Invoice payment',
    counterparty: 'merchant_abc',
  },
  {
    id: 'txn-002',
    userId: 'user_002',
    amount: 5000.0,
    currency: 'EUR',
    status: 'Pending',
    timestamp: '2024-01-15T14:33:00Z',
  },
];

async function setupMocks(page: Page) {
  await page.route('**/api/transactions', async (route) => {
    const method = route.request().method();
    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockTransactions),
      });
    } else if (method === 'POST') {
      const body = JSON.parse(route.request().postData() ?? '{}');
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'txn-new-001',
          userId: body.userId,
          amount: Number(body.amount),
          currency: body.currency,
          status: 'Pending',
          timestamp: new Date().toISOString(),
        }),
      });
    } else {
      await route.continue();
    }
  });

  await page.route('**/api/transactions/stream', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'text/event-stream',
      headers: { 'Cache-Control': 'no-cache' },
      body: '',
    });
  });
}

test.describe('Transaction Dashboard - page structure', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto('/');
  });

  test('renders the page header and live indicator', async ({ page }) => {
    await expect(page.getByTestId('app-header')).toBeVisible();
    await expect(page.getByTestId('app-header').getByText('Live', { exact: true })).toBeVisible();
  });

  test('shows all four KPI card labels', async ({ page }) => {
    await expect(page.getByText('Total Transactions')).toBeVisible();
    await expect(page.getByText('Total Volume')).toBeVisible();
    await expect(page.getByText('Average Amount')).toBeVisible();
    await expect(page.locator('div').filter({ hasText: /^Pending$/ }).first()).toBeVisible();
  });

  test('renders New Transaction form with required fields', async ({ page }) => {
    await expect(page.getByText('New Transaction')).toBeVisible();
    await expect(page.getByPlaceholder('Enter user ID')).toBeVisible();
    await expect(page.getByPlaceholder('0.00')).toBeVisible();
    await expect(page.getByRole('combobox')).toBeVisible();
  });

  test('renders Live Feed section with placeholder', async ({ page }) => {
    await expect(page.getByText('Live Feed')).toBeVisible();
    await expect(page.getByText('Waiting for real-time events...')).toBeVisible();
  });

  test('renders Transactions table with column headers', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Transactions' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /User/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /Amount/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /Currency/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /Status/i })).toBeVisible();
  });
});

test.describe('Transaction Dashboard - data display', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto('/');
  });

  test('shows loaded transactions in the table', async ({ page }) => {
    await expect(page.getByText('user_001')).toBeVisible();
    await expect(page.getByText('user_002')).toBeVisible();
    await expect(page.getByRole('cell', { name: 'USD' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'EUR' })).toBeVisible();
  });

  test('shows correct transaction count in KPI cards', async ({ page }) => {
    // Total = 2, shown as plain integer
    const totalCard = page.locator('div').filter({ hasText: /^Total Transactions$/ }).locator('..');
    await expect(totalCard.getByText('2')).toBeVisible();
  });

  test('shows correct pending count in KPI cards', async ({ page }) => {
    const pendingCard = page.locator('div').filter({ hasText: /^Pending$/ }).locator('..');
    await expect(pendingCard.getByText('1')).toBeVisible();
  });

  test('shows transaction status badges', async ({ page }) => {
    await expect(page.getByText('Completed')).toBeVisible();
    await expect(page.getByText('Pending').first()).toBeVisible();
  });

  test('shows correct transaction count in table header', async ({ page }) => {
    await expect(page.getByText('2 total')).toBeVisible();
  });
});

test.describe('Transaction Dashboard - form interaction', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await authenticate(page);
    await page.goto('/');
  });

  test('submit button is disabled when form fields are empty', async ({ page }) => {
    await expect(page.getByTestId('txn-submit')).toBeDisabled();
  });

  test('submit button enables after filling required fields', async ({ page }) => {
    await page.getByTestId('txn-user-id').fill('test_user');
    await page.getByTestId('txn-amount').fill('100');
    await expect(page.getByTestId('txn-submit')).toBeEnabled();
  });

  test('submit button stays disabled with only user ID filled', async ({ page }) => {
    await page.getByTestId('txn-user-id').fill('test_user');
    await expect(page.getByTestId('txn-submit')).toBeDisabled();
  });

  test('submit button stays disabled with only amount filled', async ({ page }) => {
    await page.getByTestId('txn-amount').fill('100');
    await expect(page.getByTestId('txn-submit')).toBeDisabled();
  });

  test('submits transaction and shows success banner', async ({ page }) => {
    await page.getByTestId('txn-user-id').fill('test_user');
    await page.getByTestId('txn-amount').fill('500');
    await page.getByTestId('txn-submit').click();
    await expect(page.getByText('Transfer submitted successfully')).toBeVisible();
  });

  test('form resets to empty after successful submission', async ({ page }) => {
    const userInput = page.getByTestId('txn-user-id');
    await userInput.fill('test_user');
    await page.getByTestId('txn-amount').fill('500');
    await page.getByTestId('txn-submit').click();
    await expect(page.getByText('Transfer submitted successfully')).toBeVisible();
    await expect(userInput).toHaveValue('');
  });

  test('new transaction appears in table after submission', async ({ page }) => {
    await page.getByTestId('txn-user-id').fill('e2e_test_user');
    await page.getByTestId('txn-amount').fill('750');
    await page.getByTestId('txn-submit').click();
    await expect(page.getByText('Transfer submitted successfully')).toBeVisible();
    await expect(page.getByText('e2e_test_user')).toBeVisible();
  });

  test('table count updates after new transaction is added', async ({ page }) => {
    await expect(page.getByText('2 total')).toBeVisible();
    await page.getByTestId('txn-user-id').fill('new_user');
    await page.getByTestId('txn-amount').fill('100');
    await page.getByTestId('txn-submit').click();
    await expect(page.getByText('Transfer submitted successfully')).toBeVisible();
    await expect(page.getByText('3 total')).toBeVisible();
  });

  test('currency dropdown has USD selected by default', async ({ page }) => {
    await expect(page.getByRole('combobox')).toHaveValue('USD');
  });

  test('currency dropdown can be changed', async ({ page }) => {
    await page.getByRole('combobox').selectOption('EUR');
    await expect(page.getByRole('combobox')).toHaveValue('EUR');
  });
});

test.describe('Transaction Dashboard - error and empty states', () => {
  test('shows empty state message when no transactions exist', async ({ page }) => {
    await page.route('**/api/transactions', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
      } else {
        await route.continue();
      }
    });
    await page.route('**/api/transactions/stream', async (route) => {
      await route.fulfill({ status: 200, contentType: 'text/event-stream', body: '' });
    });
    await authenticate(page);
    await page.goto('/');
    await expect(page.getByText('No transactions yet')).toBeVisible();
  });

  test('shows API error banner when server is unavailable', async ({ page }) => {
    await page.route('**/api/transactions', async (route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({ status: 500, body: 'Internal Server Error' });
      } else {
        await route.continue();
      }
    });
    await page.route('**/api/transactions/stream', async (route) => {
      await route.fulfill({ status: 200, contentType: 'text/event-stream', body: '' });
    });
    await authenticate(page);
    await page.goto('/');
    await expect(
      page.getByText('Failed to load transactions')
    ).toBeVisible({ timeout: 15_000 });
  });
});
