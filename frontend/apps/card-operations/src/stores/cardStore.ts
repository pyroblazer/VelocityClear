import { create } from 'zustand';

interface CardStore {
  keys: string[];
  setKeys: (keys: string[]) => void;
  addKey: (key: string) => void;
  encryptedPinBlock: string | null;
  setEncryptedPinBlock: (block: string | null) => void;
  decryptedPin: string | null;
  setDecryptedPin: (pin: string | null) => void;
  verificationResult: { verified: boolean; message: string } | null;
  setVerificationResult: (result: { verified: boolean; message: string } | null) => void;
  parsedMessage: { mti: string; fields: Record<string, string>; mtiDescription: string } | null;
  setParsedMessage: (msg: { mti: string; fields: Record<string, string>; mtiDescription: string } | null) => void;
  builtMessage: string | null;
  setBuiltMessage: (msg: string | null) => void;
  authResult: { approved: boolean; responseCode: string; authorizationId: string; message: string } | null;
  setAuthResult: (result: { approved: boolean; responseCode: string; authorizationId: string; message: string } | null) => void;
  error: string | null;
  setError: (error: string | null) => void;
  clearResults: () => void;
}

export const useCardStore = create<CardStore>((set) => ({
  keys: [],
  setKeys: (keys) => set({ keys }),
  addKey: (key) => set((state) => ({ keys: [...state.keys, key] })),
  encryptedPinBlock: null,
  setEncryptedPinBlock: (block) => set({ encryptedPinBlock: block }),
  decryptedPin: null,
  setDecryptedPin: (pin) => set({ decryptedPin: pin }),
  verificationResult: null,
  setVerificationResult: (result) => set({ verificationResult: result }),
  parsedMessage: null,
  setParsedMessage: (msg) => set({ parsedMessage: msg }),
  builtMessage: null,
  setBuiltMessage: (msg) => set({ builtMessage: msg }),
  authResult: null,
  setAuthResult: (result) => set({ authResult: result }),
  error: null,
  setError: (error) => set({ error }),
  clearResults: () => set({
    encryptedPinBlock: null,
    decryptedPin: null,
    verificationResult: null,
    parsedMessage: null,
    builtMessage: null,
    authResult: null,
    error: null,
  }),
}));
