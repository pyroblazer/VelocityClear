import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App';

vi.mock('./pages/TransactionsPage', () => ({ default: () => <div>TransactionsPage</div> }));
vi.mock('./pages/AdminPage', () => ({ default: () => <div>AdminPage</div> }));
vi.mock('./pages/RiskPage', () => ({ default: () => <div>RiskPage</div> }));
vi.mock('./pages/AuditPage', () => ({ default: () => <div>AuditPage</div> }));
vi.mock('./pages/CardOperationsPage', () => ({ default: () => <div>CardOperationsPage</div> }));

function renderApp(initialPath = '/') {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[initialPath]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('App layout', () => {
  it('renders VelocityClear brand in header', () => {
    renderApp();
    expect(screen.getByText('VelocityClear')).toBeInTheDocument();
  });

  it('renders all five sidebar nav links', () => {
    renderApp();
    expect(screen.getByTestId('nav-transactions')).toBeInTheDocument();
    expect(screen.getByTestId('nav-admin')).toBeInTheDocument();
    expect(screen.getByTestId('nav-risk')).toBeInTheDocument();
    expect(screen.getByTestId('nav-audit')).toBeInTheDocument();
    expect(screen.getByTestId('nav-cards')).toBeInTheDocument();
  });

  it('redirects / to /transactions and shows TransactionsPage', () => {
    renderApp('/');
    expect(screen.getByText('TransactionsPage')).toBeInTheDocument();
  });

  it('shows AdminPage when navigating to /admin', () => {
    renderApp('/admin');
    expect(screen.getByText('AdminPage')).toBeInTheDocument();
  });

  it('shows RiskPage when navigating to /risk', () => {
    renderApp('/risk');
    expect(screen.getByText('RiskPage')).toBeInTheDocument();
  });

  it('shows AuditPage when navigating to /audit', () => {
    renderApp('/audit');
    expect(screen.getByText('AuditPage')).toBeInTheDocument();
  });

  it('shows CardOperationsPage when navigating to /cards', () => {
    renderApp('/cards');
    expect(screen.getByText('CardOperationsPage')).toBeInTheDocument();
  });
});
