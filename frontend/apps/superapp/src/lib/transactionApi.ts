import { getJson, postJson } from './apiClient';

interface Transaction {
  id: string;
  userId: string;
  amount: number;
  currency: string;
  status: string;
  timestamp: string;
  description?: string;
  counterparty?: string;
  pan?: string;
  cardType?: string;
  error?: string;
}

export async function fetchTransactions() {
  return getJson<Transaction[]>('/api/transactions');
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
  return postJson<Transaction>('/api/transactions', data);
}
