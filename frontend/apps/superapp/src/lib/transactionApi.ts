import { getJson, postJson } from './apiClient';

export async function fetchTransactions() {
  return getJson<unknown[]>('/api/transactions');
}

export async function createTransaction(data: {
  userId: string;
  amount: number;
  currency: string;
  description?: string;
  counterparty?: string;
  pan?: string;
  pinBlock?: string;
  cardType?: string;
}) {
  return postJson<unknown>('/api/transactions', data);
}
