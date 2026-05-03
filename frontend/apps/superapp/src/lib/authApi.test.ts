import { describe, it, expect, vi, beforeEach } from 'vitest';
import { login, register } from './authApi';

describe('authApi', () => {
  beforeEach(() => { vi.restoreAllMocks(); });

  it('login calls POST /api/auth/login with credentials', async () => {
    const mock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ token: 'abc', role: 'Admin', expiresAt: '2026-01-01' }), { status: 200 }));
    vi.stubGlobal('fetch', mock);

    const result = await login('admin', 'admin123');
    expect(mock).toHaveBeenCalledWith('/api/auth/login', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ username: 'admin', password: 'admin123' }),
    }));
    expect(result.token).toBe('abc');
  });

  it('login throws on 401', async () => {
    vi.stubGlobal('fetch', () => Promise.resolve(new Response('Unauthorized', { status: 401 })));
    await expect(login('bad', 'creds')).rejects.toThrow('Login failed');
  });

  it('register calls POST /api/auth/register', async () => {
    const mock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ token: 'new', role: 'User', expiresAt: '2026-01-01' }), { status: 200 }));
    vi.stubGlobal('fetch', mock);

    await register('newuser', 'pass123', 'User');
    expect(mock).toHaveBeenCalledWith('/api/auth/register', expect.objectContaining({
      body: JSON.stringify({ username: 'newuser', password: 'pass123', role: 'User' }),
    }));
  });
});
