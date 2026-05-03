import { useAuthStore } from '../stores/authStore';

export async function fetchWithAuth(path: string, init?: RequestInit): Promise<Response> {
  const token = useAuthStore.getState().token;
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init?.headers as Record<string, string> ?? {}),
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const res = await fetch(path, { ...init, headers });

  if (res.status === 401) {
    useAuthStore.getState().logout();
  }
  return res;
}

export async function getJson<T>(path: string): Promise<T> {
  const res = await fetchWithAuth(path);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export async function postJson<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetchWithAuth(path, {
    method: 'POST',
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`${res.status} ${res.statusText}${text ? ': ' + text : ''}`);
  }
  return res.json();
}

export async function putJson<T>(path: string, body?: unknown): Promise<T> {
  const res = await fetchWithAuth(path, {
    method: 'PUT',
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export async function delJson<T>(path: string): Promise<T> {
  const res = await fetchWithAuth(path, { method: 'DELETE' });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}
