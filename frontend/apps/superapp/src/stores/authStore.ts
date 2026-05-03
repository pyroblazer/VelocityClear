import { create } from 'zustand';

interface AuthState {
  token: string | null;
  role: string | null;
  expiresAt: Date | null;
  isAuthenticated: boolean;
  login: (token: string, role: string, expiresAt: Date) => void;
  logout: () => void;
}

const STORAGE_KEY = 'vc_auth';

function loadFromStorage(): Pick<AuthState, 'token' | 'role' | 'expiresAt'> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return { token: null, role: null, expiresAt: null };
    const data = JSON.parse(raw);
    if (data.expiresAt && new Date(data.expiresAt) > new Date()) {
      return { token: data.token, role: data.role, expiresAt: new Date(data.expiresAt) };
    }
    localStorage.removeItem(STORAGE_KEY);
    return { token: null, role: null, expiresAt: null };
  } catch {
    return { token: null, role: null, expiresAt: null };
  }
}

function computeAuthenticated(token: string | null, expiresAt: Date | null): boolean {
  return !!token && !!expiresAt && expiresAt > new Date();
}

const stored = loadFromStorage();

export const useAuthStore = create<AuthState>((set) => ({
  token: stored.token,
  role: stored.role,
  expiresAt: stored.expiresAt,
  isAuthenticated: computeAuthenticated(stored.token, stored.expiresAt),

  login: (token, role, expiresAt) => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ token, role, expiresAt: expiresAt.toISOString() }));
    set({ token, role, expiresAt, isAuthenticated: true });
  },

  logout: () => {
    localStorage.removeItem(STORAGE_KEY);
    set({ token: null, role: null, expiresAt: null, isAuthenticated: false });
  },
}));
