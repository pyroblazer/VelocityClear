const BASE = '/api';

export async function fetchTransactions() {
  const res = await fetch(`${BASE}/transactions`);
  return res.json();
}

export async function createTransaction(data: {
  userId: string;
  amount: number;
  currency: string;
  description?: string;
  counterparty?: string;
}) {
  const res = await fetch(`${BASE}/transactions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  });
  return res.json();
}
