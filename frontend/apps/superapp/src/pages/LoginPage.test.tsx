import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import LoginPage from './LoginPage';

function renderLogin() {
  return render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('renders login form by default', () => {
    renderLogin();
    expect(screen.getAllByText('Sign In').length).toBeGreaterThan(0);
    expect(screen.getByText('Username')).toBeInTheDocument();
    expect(screen.getByText('Password')).toBeInTheDocument();
  });

  it('toggles to register form', () => {
    renderLogin();
    fireEvent.click(screen.getByText('Create one'));
    expect(screen.getAllByText('Create Account').length).toBeGreaterThan(0);
  });

  it('shows error on failed login', async () => {
    vi.stubGlobal('fetch', () => Promise.resolve(new Response('Invalid', { status: 401 })));
    const { container } = renderLogin();

    const usernameInput = container.querySelector('input[type="text"]') as HTMLInputElement;
    const passwordInput = container.querySelector('input[type="password"]') as HTMLInputElement;

    fireEvent.change(usernameInput, { target: { value: 'bad' } });
    fireEvent.change(passwordInput, { target: { value: 'wrong' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByText(/Login failed/i)).toBeInTheDocument();
    });
  });

  it('shows default credentials hint', () => {
    renderLogin();
    expect(screen.getByText('admin / admin123')).toBeInTheDocument();
  });
});
