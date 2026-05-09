import { describe, it, expect, beforeEach, vi } from 'vitest';
import { getJson, postJson } from './apiClient';
import { useAuthStore } from '../stores/authStore';

describe('apiClient', () => {
  beforeEach(() => {
    localStorage.clear();
    useAuthStore.setState({ token: null, role: null, expiresAt: null, isAuthenticated: false });
    vi.restoreAllMocks();
  });

  it('attaches Authorization header when token exists', async () => {
    useAuthStore.setState({ token: 'test-jwt', role: 'Admin', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    const mock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true }), { status: 200 }));
    vi.stubGlobal('fetch', mock);

    await getJson('/api/test');
    expect(mock).toHaveBeenCalledWith('/api/test', expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer test-jwt' }),
    }));
  });

  it('calls logout on 401 response', async () => {
    useAuthStore.setState({ token: 'will-expire', role: 'User', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    vi.stubGlobal('fetch', () => Promise.resolve(new Response('', { status: 401 })));

    try { await getJson('/api/protected'); } catch { /* expected 401 */ }
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
    expect(useAuthStore.getState().token).toBeNull();
  });

  it('sends JSON body with POST', async () => {
    const mock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: '1' }), { status: 200 }));
    vi.stubGlobal('fetch', mock);

    await postJson('/api/items', { name: 'test' });
    expect(mock).toHaveBeenCalledWith('/api/items', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ name: 'test' }),
    }));
  });

  it('throws on non-OK response', async () => {
    vi.stubGlobal('fetch', () => Promise.resolve(new Response('Not Found', { status: 404 })));
    await expect(getJson('/api/missing')).rejects.toThrow('404');
  });
});
