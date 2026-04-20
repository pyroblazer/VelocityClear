const BASE = '/api';

export async function listHsmKeys() {
  const res = await fetch(`${BASE}/hsm/keys`);
  if (!res.ok) throw new Error('Failed to list keys');
  return res.json();
}

export async function generateKey(keyType: string, keyId: string) {
  const res = await fetch(`${BASE}/hsm/keys/generate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ keyType, keyId }),
  });
  if (!res.ok) {
    const data = await res.json();
    throw new Error(data.error || 'Failed to generate key');
  }
  return res.json();
}

export async function encryptPin(pin: string, pan: string, zpkId: string) {
  const res = await fetch(`${BASE}/hsm/pin/encrypt`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ pin, pan, zpkId }),
  });
  if (!res.ok) {
    const data = await res.json();
    throw new Error(data.error || 'Failed to encrypt PIN');
  }
  return res.json();
}

export async function decryptPin(encryptedPinBlock: string, pan: string, zpkId: string) {
  const res = await fetch(`${BASE}/hsm/pin/decrypt`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ encryptedPinBlock, pan, zpkId }),
  });
  if (!res.ok) {
    const data = await res.json();
    throw new Error(data.error || 'Failed to decrypt PIN');
  }
  return res.json();
}

export async function verifyPin(encryptedPinBlock: string, pan: string, zpkId: string, expectedPin: string) {
  const res = await fetch(`${BASE}/hsm/pin/verify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ encryptedPinBlock, pan, zpkId, expectedPin }),
  });
  if (!res.ok) {
    const data = await res.json();
    throw new Error(data.error || 'Failed to verify PIN');
  }
  return res.json();
}

export async function parseIso8583(isoMessage: string) {
  const res = await fetch(`${BASE}/iso8583/parse`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ isoMessage }),
  });
  if (!res.ok) {
    const data = await res.json();
    throw new Error(data.error || 'Failed to parse message');
  }
  return res.json();
}

export async function buildIso8583(mti: string, fields: Record<string, string>) {
  const res = await fetch(`${BASE}/iso8583/build`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ mti, fields }),
  });
  if (!res.ok) {
    const data = await res.json();
    throw new Error(data.error || 'Failed to build message');
  }
  return res.json();
}

export async function authorizeCard(request: {
  pan: string;
  amount: number;
  currency: string;
  encryptedPinBlock: string;
  zpkId: string;
  terminalId: string;
  merchantId: string;
}) {
  const res = await fetch(`${BASE}/iso8583/authorize`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
  if (!res.ok) {
    const data = await res.json();
    throw new Error(data.error || 'Authorization failed');
  }
  return res.json();
}
