import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import AdminPage from './AdminPage';

vi.mock('../lib/gatewayApi', () => ({
  gatewayApi: {
    getHealth: vi.fn(),
    getStatus: vi.fn(),
  },
  auditApi: {
    getStats: vi.fn(),
  },
}));

import { gatewayApi, auditApi } from '../lib/gatewayApi';

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <AdminPage />
    </QueryClientProvider>,
  );
}

const mockHealth = {
  status: 'Healthy',
  services: {
    'Transaction Service': { name: 'Transaction Service', status: 'Healthy', url: 'http://localhost:5001', responseTime: 12 },
    'Risk Service': { name: 'Risk Service', status: 'Healthy', url: 'http://localhost:5002', responseTime: 8 },
    'Payment Service': { name: 'Payment Service', status: 'Degraded', url: 'http://localhost:5003', responseTime: 45 },
  },
};

const mockStatus = { activeSseConnections: 3, eventBusBackend: 'Redis' };
const mockStats = { totalEvents: 142, eventsByType: { TransactionCreated: 80, RiskEvaluated: 62 } };

describe('AdminPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.mocked(gatewayApi.getHealth).mockResolvedValue(mockHealth);
    vi.mocked(gatewayApi.getStatus).mockResolvedValue(mockStatus);
    vi.mocked(auditApi.getStats).mockResolvedValue(mockStats);
  });

  it('renders admin header', async () => {
    renderPage();
    expect(screen.getByText('Admin Control Panel')).toBeInTheDocument();
  });

  it('shows service statuses from API', async () => {
    renderPage();
    expect(await screen.findByText('Transaction Service')).toBeInTheDocument();
    expect(screen.getByText('Risk Service')).toBeInTheDocument();
    expect(screen.getByText('Payment Service')).toBeInTheDocument();
    expect(screen.getAllByText('Healthy').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('Degraded')).toBeInTheDocument();
  });

  it('shows event bus backend and SSE connections', async () => {
    renderPage();
    expect(await screen.findByText('Redis')).toBeInTheDocument();
    expect(await screen.findByText('active streams')).toBeInTheDocument();
  });

  it('health check button refetches', async () => {
    renderPage();
    expect(await screen.findByText('Run Health Check')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('health-check-btn'));
    expect(gatewayApi.getHealth).toHaveBeenCalledTimes(2);
  });

  it('shows metrics panel on View Metrics click', async () => {
    renderPage();
    expect(await screen.findByText('View Metrics')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('metrics-btn'));
    expect(screen.getByText('Total Events')).toBeInTheDocument();
    expect(screen.getByText('142')).toBeInTheDocument();
    expect(screen.getByText('Event Types')).toBeInTheDocument();
  });

  it('shows error banner when health fetch fails', async () => {
    vi.mocked(gatewayApi.getHealth).mockRejectedValue(new Error('Network error'));
    renderPage();
    expect(await screen.findByText(/Failed to load service health/)).toBeInTheDocument();
  });
});
