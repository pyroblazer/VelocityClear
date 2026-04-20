import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockFetch = vi.fn();
global.fetch = mockFetch;

const API_BASE = '/api';

describe('API functions', () => {
  beforeEach(() => {
    mockFetch.mockReset();
  });

  it('getHsmHealth returns health data', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ service: 'PinEncryptionService', status: 'Healthy', keyCount: 1 }),
    });

    const { getHsmHealth } = await import('./api');
    const result = await getHsmHealth();
    expect(result.service).toBe('PinEncryptionService');
    expect(mockFetch).toHaveBeenCalledWith(`${API_BASE}/hsm/health`);
  });

  it('listHsmKeys returns key IDs', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ keyIds: ['default-zpk', 'test-key'] }),
    });

    const { listHsmKeys } = await import('./api');
    const result = await listHsmKeys();
    expect(result.keyIds).toEqual(['default-zpk', 'test-key']);
  });

  it('generateKey sends POST and returns result', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ keyId: 'new-key', keyType: 'ZPK', keyCheckValue: 'ABC123' }),
    });

    const { generateKey } = await import('./api');
    const result = await generateKey('ZPK', 'new-key');
    expect(result.keyId).toBe('new-key');
    expect(mockFetch).toHaveBeenCalledWith(
      `${API_BASE}/hsm/keys/generate`,
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('encryptPin sends correct payload', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ encryptedPinBlock: 'AABB112233445566', keyCheckValue: 'XYZ', format: 'ISO9564-0' }),
    });

    const { encryptPin } = await import('./api');
    const result = await encryptPin('1234', '4111111111111111', 'default-zpk');
    expect(result.encryptedPinBlock).toBe('AABB112233445566');

    const call = mockFetch.mock.calls[0];
    const body = JSON.parse(call[1].body);
    expect(body).toEqual({ pin: '1234', pan: '4111111111111111', zpkId: 'default-zpk' });
  });

  it('throws on error response', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      json: () => Promise.resolve({ error: 'Key not found' }),
    });

    const { decryptPin } = await import('./api');
    await expect(decryptPin('block', 'pan', 'no-key')).rejects.toThrow('Key not found');
  });

  it('parseIso8583 sends POST request', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ mti: '0100', fields: {}, mtiDescription: 'Authorization Request' }),
    });

    const { parseIso8583 } = await import('./api');
    const result = await parseIso8583('0100...');
    expect(result.mti).toBe('0100');
  });

  it('authorizeCard sends full request', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({ approved: true, responseCode: '00', authorizationId: '123', message: 'Approved' }),
    });

    const { authorizeCard } = await import('./api');
    const result = await authorizeCard({
      pan: '4111111111111111',
      amount: 100,
      currency: 'USD',
      encryptedPinBlock: 'block',
      zpkId: 'default-zpk',
      terminalId: 'TERM001',
      merchantId: 'MERCH001',
    });
    expect(result.approved).toBe(true);
  });
});
