export interface LoginResponse {
  token: string;
  role: string;
  expiresAt: string;
}

export async function login(username: string, password: string): Promise<LoginResponse> {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`Login failed: ${res.status}${text ? ' — ' + text : ''}`);
  }
  return res.json();
}

export async function register(username: string, password: string, role: string): Promise<LoginResponse> {
  const res = await fetch('/api/auth/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password, role }),
  });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`Registration failed: ${res.status}${text ? ' — ' + text : ''}`);
  }
  return res.json();
}
