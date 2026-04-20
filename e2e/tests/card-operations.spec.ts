import { test, expect, type Page } from '@playwright/test';

const BASE_URL = '/cards';

const mockHsmHealth = {
  service: 'PinEncryptionService',
  status: 'Healthy',
  keyCount: 1,
  timestamp: new Date().toISOString(),
};

const mockKeys = { keyIds: ['default-zpk'] };

const mockEncryptResult = {
  encryptedPinBlock: 'AABBCCDDEEFF0011',
  keyCheckValue: 'ABC123',
  format: 'ISO9564-0',
};

const mockDecryptResult = { pin: '1234', format: 'ISO9564-0' };

const mockVerifyTrue = { verified: true, message: 'PIN correct' };
const mockVerifyFalse = { verified: false, message: 'PIN mismatch' };

const mockParsedIso = {
  mti: '0100',
  fields: { '2': '4111111111111111', '4': '000000001000', '49': 'USD' },
  mtiDescription: 'Authorization Request',
};

const mockBuiltIso = {
  isoMessage: '0100F0000000000000000000000000000000000012341111111111111111000000001000USD',
  length: 80,
};

const mockAuthApproved = {
  approved: true,
  responseCode: '00',
  authorizationId: '654321',
  isoMessage: '0110...',
  message: 'Approved',
};

const mockAuthDeclined = {
  approved: false,
  responseCode: '05',
  authorizationId: '',
  isoMessage: '0110...',
  message: 'Declined - Do not honour',
};

const mockGeneratedKey = {
  keyId: 'test-zpk-002',
  keyType: 'ZPK',
  keyCheckValue: 'DEF456',
  encryptedUnderLmk: 'AABBCCDD',
};

async function setupMocks(page: Page) {
  await page.route('**/api/hsm/health', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockHsmHealth),
    });
  });

  await page.route('**/api/hsm/keys', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockKeys),
      });
    } else {
      await route.continue();
    }
  });

  await page.route('**/api/hsm/keys/generate', async (route) => {
    const body = JSON.parse(route.request().postData() ?? '{}');
    if (body.keyId === 'default-zpk') {
      await route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ error: "Key 'default-zpk' already exists." }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...mockGeneratedKey, keyId: body.keyId }),
      });
    }
  });

  await page.route('**/api/hsm/pin/encrypt', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockEncryptResult),
    });
  });

  await page.route('**/api/hsm/pin/decrypt', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockDecryptResult),
    });
  });

  await page.route('**/api/hsm/pin/verify', async (route) => {
    const body = JSON.parse(route.request().postData() ?? '{}');
    const verified = body.expectedPin === '1234';
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(verified ? mockVerifyTrue : mockVerifyFalse),
    });
  });

  await page.route('**/api/iso8583/fields', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });

  await page.route('**/api/iso8583/parse', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockParsedIso),
    });
  });

  await page.route('**/api/iso8583/build', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockBuiltIso),
    });
  });

  await page.route('**/api/iso8583/authorize', async (route) => {
    const body = JSON.parse(route.request().postData() ?? '{}');
    const approved = !body.pan?.startsWith('4999');
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(approved ? mockAuthApproved : mockAuthDeclined),
    });
  });
}

test.describe('Card Operations - page structure', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await page.goto(BASE_URL);
  });

  test('renders the page header with Shield icon', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Card Operations' })).toBeVisible();
  });

  test('shows HSM Key Management section', async ({ page }) => {
    await expect(page.getByText('HSM Key Management')).toBeVisible();
  });

  test('shows PIN Operations section', async ({ page }) => {
    await expect(page.getByText('PIN Operations')).toBeVisible();
  });

  test('shows ISO 8583 Tools section', async ({ page }) => {
    await expect(page.getByText('ISO 8583 Tools')).toBeVisible();
  });

  test('displays default ZPK key in key list', async ({ page }) => {
    await expect(page.getByText('default-zpk')).toBeVisible();
  });

  test('shows ISO 8583 subtitle in header', async ({ page }) => {
    const header = page.locator('header');
    await expect(header.getByText('ISO 8583')).toBeVisible();
  });
});

test.describe('Card Operations - HSM key management', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await page.goto(BASE_URL);
  });

  test('generates a new key and shows success', async ({ page }) => {
    const keyInput = page.getByTestId('key-id-input');
    await keyInput.fill('test-zpk-new');
    await page.getByTestId('generate-key-btn').click();
    await expect(page.getByText(/Generated ZPK key: test-zpk-new/)).toBeVisible();
  });

  test('shows error when generating duplicate key', async ({ page }) => {
    const keyInput = page.getByTestId('key-id-input');
    await keyInput.fill('default-zpk');
    await page.getByTestId('generate-key-btn').click();
    await expect(page.getByText(/already exists/).first()).toBeVisible();
  });

  test('generate button is disabled when key ID is empty', async ({ page }) => {
    await expect(page.getByTestId('generate-key-btn')).toBeDisabled();
  });
});

test.describe('Card Operations - PIN operations', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await page.goto(BASE_URL);
  });

  test('encrypts a PIN and displays the encrypted block', async ({ page }) => {
    await page.getByTestId('encrypt-pin-input').fill('1234');
    await page.getByTestId('encrypt-pan-input').fill('4111111111111111');
    await page.getByTestId('encrypt-btn').click();
    await expect(page.getByText('AABBCCDDEEFF0011')).toBeVisible();
  });

  test('decrypts an encrypted PIN block', async ({ page }) => {
    await page.getByTestId('decrypt-block-input').fill('AABBCCDDEEFF0011');
    await page.getByTestId('decrypt-pan-input').fill('4111111111111111');
    await page.getByTestId('decrypt-btn').click();
    await expect(page.getByText(/PIN: 1234/)).toBeVisible();
  });

  test('verifies a correct PIN and shows verified', async ({ page }) => {
    await page.getByTestId('verify-block-input').fill('AABBCCDDEEFF0011');
    await page.getByTestId('verify-pan-input').fill('4111111111111111');
    await page.getByTestId('verify-expected-input').fill('1234');
    await page.getByTestId('verify-btn').click();
    await expect(page.getByText('PIN Verified')).toBeVisible();
  });

  test('verifies an incorrect PIN and shows mismatch', async ({ page }) => {
    await page.getByTestId('verify-block-input').fill('AABBCCDDEEFF0011');
    await page.getByTestId('verify-pan-input').fill('4111111111111111');
    await page.getByTestId('verify-expected-input').fill('9999');
    await page.getByTestId('verify-btn').click();
    await expect(page.getByText('PIN Mismatch')).toBeVisible();
  });

  test('encrypt button is disabled when fields are empty', async ({ page }) => {
    await expect(page.getByTestId('encrypt-btn')).toBeDisabled();
  });
});

test.describe('Card Operations - ISO 8583 tools', () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
    await page.goto(BASE_URL);
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

  test('authorizes a card with valid PAN and shows approved', async ({ page }) => {
    await page.getByTestId('auth-pan-input').fill('4111111111111111');
    await page.getByTestId('auth-amount-input').fill('100.00');
    await page.getByTestId('authorize-btn').click();
    await expect(page.getByText('APPROVED').first()).toBeVisible();
  });

  test('declines a card with deny-listed PAN', async ({ page }) => {
    await page.getByTestId('auth-pan-input').clear();
    await page.getByTestId('auth-pan-input').fill('4999111111111111');
    await page.getByTestId('authorize-btn').click();
    await expect(page.getByText('DECLINED').first()).toBeVisible();
  });
});

test.describe('Card Operations - error states', () => {
  test('shows error when HSM is unreachable', async ({ page }) => {
    await page.route('**/api/hsm/**', async (route) => {
      await route.fulfill({ status: 500, body: 'Internal Server Error' });
    });
    await page.route('**/api/iso8583/**', async (route) => {
      await route.fulfill({ status: 500, body: 'Internal Server Error' });
    });
    await page.goto(BASE_URL);
    await expect(page.getByText(/Failed to connect to HSM/i).first()).toBeVisible({ timeout: 15_000 });
  });
});
