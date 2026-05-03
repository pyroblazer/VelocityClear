import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import ProtectedRoute from './ProtectedRoute';
import { useAuthStore } from '../stores/authStore';

function renderWithRouter(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<div data-testid="login-page">Login</div>} />
        <Route element={<ProtectedRoute />}>
          <Route path="/protected" element={<div data-testid="protected-content">Protected</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute', () => {
  beforeEach(() => {
    localStorage.clear();
    useAuthStore.setState({ token: null, role: null, expiresAt: null, isAuthenticated: false });
  });

  it('redirects to /login when unauthenticated', () => {
    renderWithRouter('/protected');
    expect(screen.getByTestId('login-page')).toBeInTheDocument();
  });

  it('renders children when authenticated', () => {
    useAuthStore.setState({ token: 'valid', role: 'Admin', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    renderWithRouter('/protected');
    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
  });
});
