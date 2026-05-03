import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App';
import { useAuthStore } from './stores/authStore';

vi.mock('./pages/TransactionsPage', () => ({ default: () => <div>TransactionsPage</div> }));
vi.mock('./pages/AdminPage', () => ({ default: () => <div>AdminPage</div> }));
vi.mock('./pages/RiskPage', () => ({ default: () => <div>RiskPage</div> }));
vi.mock('./pages/AuditPage', () => ({ default: () => <div>AuditPage</div> }));
vi.mock('./pages/CardOperationsPage', () => ({ default: () => <div>CardOperationsPage</div> }));
vi.mock('./pages/ComplianceDashboardPage', () => ({ default: () => <div>ComplianceDashboardPage</div> }));
vi.mock('./pages/KycPage', () => ({ default: () => <div>KycPage</div> }));
vi.mock('./pages/ConsentPage', () => ({ default: () => <div>ConsentPage</div> }));
vi.mock('./pages/SettingsPage', () => ({ default: () => <div>SettingsPage</div> }));

function renderApp(initialPath = '/') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[initialPath]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('App layout', () => {
  beforeEach(() => {
    localStorage.clear();
    useAuthStore.setState({ token: null, role: null, expiresAt: null, isAuthenticated: false });
  });

  it('redirects to /login when unauthenticated', () => {
    renderApp('/transactions');
    expect(screen.getAllByText('Sign In').length).toBeGreaterThan(0);
  });

  it('shows LoginPage at /login', () => {
    renderApp('/login');
    expect(screen.getAllByText('Sign In').length).toBeGreaterThan(0);
  });

  it('renders protected layout when authenticated', () => {
    useAuthStore.setState({ token: 'valid', role: 'Admin', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    renderApp('/');
    expect(screen.getByText('VelocityClear')).toBeInTheDocument();
    expect(screen.getByText('TransactionsPage')).toBeInTheDocument();
  });

  it('shows AdminPage at /admin when authenticated', () => {
    useAuthStore.setState({ token: 'valid', role: 'Admin', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    renderApp('/admin');
    expect(screen.getByText('AdminPage')).toBeInTheDocument();
  });

  it('shows RiskPage at /risk when authenticated', () => {
    useAuthStore.setState({ token: 'valid', role: 'Admin', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    renderApp('/risk');
    expect(screen.getByText('RiskPage')).toBeInTheDocument();
  });

  it('shows AuditPage at /audit when authenticated', () => {
    useAuthStore.setState({ token: 'valid', role: 'Admin', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    renderApp('/audit');
    expect(screen.getByText('AuditPage')).toBeInTheDocument();
  });

  it('shows SettingsPage at /settings when authenticated', () => {
    useAuthStore.setState({ token: 'valid', role: 'Admin', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    renderApp('/settings');
    expect(screen.getByText('SettingsPage')).toBeInTheDocument();
  });

  it('shows all nav items when authenticated', () => {
    useAuthStore.setState({ token: 'valid', role: 'Admin', expiresAt: new Date(Date.now() + 3600000), isAuthenticated: true });
    renderApp();
    expect(screen.getByTestId('nav-transactions')).toBeInTheDocument();
    expect(screen.getByTestId('nav-admin')).toBeInTheDocument();
    expect(screen.getByTestId('nav-risk')).toBeInTheDocument();
    expect(screen.getByTestId('nav-audit')).toBeInTheDocument();
    expect(screen.getByTestId('nav-cards')).toBeInTheDocument();
    expect(screen.getByTestId('nav-compliance')).toBeInTheDocument();
    expect(screen.getByTestId('nav-settings')).toBeInTheDocument();
  });
});
