import { getJson, postJson } from './apiClient';

export async function listHsmKeys() {
  return getJson<string[]>('/api/hsm/keys');
}

export async function generateKey(keyType: string, keyId: string) {
  return postJson<{ keyId: string; keyType: string; keyCheckValue: string }>('/api/hsm/keys/generate', { keyType, keyId });
}

export async function encryptPin(pin: string, pan: string, zpkId: string) {
  return postJson<{ encryptedPinBlock: string }>('/api/hsm/pin/encrypt', { pin, pan, zpkId });
}

export async function decryptPin(encryptedPinBlock: string, pan: string, zpkId: string) {
  return postJson<{ pin: string }>('/api/hsm/pin/decrypt', { encryptedPinBlock, pan, zpkId });
}

export async function verifyPin(encryptedPinBlock: string, pan: string, zpkId: string, expectedPin: string) {
  return postJson<{ verified: boolean; message: string }>('/api/hsm/pin/verify', { encryptedPinBlock, pan, zpkId, expectedPin });
}

export async function parseIso8583(isoMessage: string) {
  return postJson<{ mti: string; fields: Record<string, string>; mtiDescription: string }>('/api/iso8583/parse', { isoMessage });
}

export async function buildIso8583(mti: string, fields: Record<string, string>) {
  return postJson<{ isoMessage: string }>('/api/iso8583/build', { mti, fields });
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
  return postJson<{ approved: boolean; responseCode: string; authorizationId: string; message: string }>('/api/iso8583/authorize', request);
}
