import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { fetchTransactions, createTransaction } from './api';

describe('api', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('fetchTransactions', () => {
    it('calls GET /api/transactions', async () => {
      const mockFetch = vi.mocked(fetch);
      mockFetch.mockResolvedValueOnce({
        json: async () => [],
      } as Response);

      await fetchTransactions();

      expect(mockFetch).toHaveBeenCalledWith('/api/transactions');
    });

    it('returns parsed JSON', async () => {
      const data = [{ id: 'tx_1', amount: 100 }];
      vi.mocked(fetch).mockResolvedValueOnce({
        json: async () => data,
      } as Response);

      const result = await fetchTransactions();
      expect(result).toEqual(data);
    });

    it('propagates fetch errors', async () => {
      vi.mocked(fetch).mockRejectedValueOnce(new Error('Network error'));
      await expect(fetchTransactions()).rejects.toThrow('Network error');
    });
  });

  describe('createTransaction', () => {
    it('calls POST /api/transactions with JSON body', async () => {
      const mockFetch = vi.mocked(fetch);
      mockFetch.mockResolvedValueOnce({
        json: async () => ({ id: 'tx_new' }),
      } as Response);

      const payload = { userId: 'u1', amount: 250, currency: 'USD' };
      await createTransaction(payload);

      expect(mockFetch).toHaveBeenCalledWith('/api/transactions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
    });

    it('includes optional description and counterparty', async () => {
      vi.mocked(fetch).mockResolvedValueOnce({
        json: async () => ({}),
      } as Response);

      const payload = {
        userId: 'u1',
        amount: 100,
        currency: 'EUR',
        description: 'Payment',
        counterparty: 'Vendor',
      };
      await createTransaction(payload);

      const call = vi.mocked(fetch).mock.calls[0];
      const body = JSON.parse((call[1] as RequestInit).body as string);
      expect(body.description).toBe('Payment');
      expect(body.counterparty).toBe('Vendor');
    });

    it('returns parsed JSON response', async () => {
      const responseData = { id: 'tx_new', status: 'Pending' };
      vi.mocked(fetch).mockResolvedValueOnce({
        json: async () => responseData,
      } as Response);

      const result = await createTransaction({ userId: 'u1', amount: 50, currency: 'USD' });
      expect(result).toEqual(responseData);
    });

    it('propagates fetch errors', async () => {
      vi.mocked(fetch).mockRejectedValueOnce(new Error('Server down'));
      await expect(
        createTransaction({ userId: 'u1', amount: 100, currency: 'USD' })
      ).rejects.toThrow('Server down');
    });
  });
});
