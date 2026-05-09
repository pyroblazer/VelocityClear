import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ComplianceDashboardPage from './ComplianceDashboardPage';

vi.mock('../lib/complianceApi', () => ({
  amlApi: {
    listAlerts: vi.fn(),
    fileSar: vi.fn(),
  },
  approvalApi: {
    list: vi.fn(),
    create: vi.fn(),
  },
  complaintApi: {
    list: vi.fn(),
    create: vi.fn(),
  },
  socApi: {
    dashboard: vi.fn(),
    createIncident: vi.fn(),
  },
}));

import { amlApi, approvalApi, complaintApi, socApi } from '../lib/complianceApi';

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <ComplianceDashboardPage />
    </QueryClientProvider>,
  );
}

describe('ComplianceDashboardPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.mocked(amlApi.listAlerts).mockResolvedValue([]);
    vi.mocked(approvalApi.list).mockResolvedValue([]);
    vi.mocked(complaintApi.list).mockResolvedValue([]);
    vi.mocked(socApi.dashboard).mockResolvedValue({ openIncidents: 0 });
  });

  it('renders compliance header and stat cards', async () => {
    renderPage();
    expect(screen.getByText('OJK Compliance Overview')).toBeInTheDocument();
    expect(await screen.findByText('Simulation Controls')).toBeInTheDocument();
  });

  it('shows simulation buttons', async () => {
    renderPage();
    expect(await screen.findByText('Simulate AML Alert')).toBeInTheDocument();
    expect(screen.getByText('Simulate Pending Approval')).toBeInTheDocument();
    expect(screen.getByText('Simulate SLA Breach')).toBeInTheDocument();
    expect(screen.getByText('Simulate Security Incident')).toBeInTheDocument();
  });

  it('simulate AML alert calls fileSar', async () => {
    vi.mocked(amlApi.fileSar).mockResolvedValue({} as never);
    renderPage();
    fireEvent.click(await screen.findByText('Simulate AML Alert'));
    await waitFor(() => {
      expect(amlApi.fileSar).toHaveBeenCalledWith(expect.objectContaining({
        userId: 'sim-user',
        suspiciousAmount: 75000,
      }));
    });
  });

  it('simulate pending approval calls create', async () => {
    vi.mocked(approvalApi.create).mockResolvedValue({} as never);
    renderPage();
    fireEvent.click(await screen.findByText('Simulate Pending Approval'));
    await waitFor(() => {
      expect(approvalApi.create).toHaveBeenCalledWith(expect.objectContaining({
        approvalType: 'TransactionApproval',
      }));
    });
  });

  it('simulate SLA breach calls complaintApi.create', async () => {
    vi.mocked(complaintApi.create).mockResolvedValue({} as never);
    renderPage();
    fireEvent.click(await screen.findByText('Simulate SLA Breach'));
    await waitFor(() => {
      expect(complaintApi.create).toHaveBeenCalledWith(expect.objectContaining({
        subject: 'SLA test complaint — auto-breach',
      }));
    });
  });

  it('simulate security incident calls socApi.createIncident', async () => {
    vi.mocked(socApi.createIncident).mockResolvedValue({} as never);
    renderPage();
    fireEvent.click(await screen.findByText('Simulate Security Incident'));
    await waitFor(() => {
      expect(socApi.createIncident).toHaveBeenCalledWith(expect.objectContaining({
        severity: 'Critical',
      }));
    });
  });

  it('shows success feedback after simulation', async () => {
    vi.mocked(amlApi.fileSar).mockResolvedValue({} as never);
    renderPage();
    fireEvent.click(await screen.findByText('Simulate AML Alert'));
    expect(await screen.findByText('AML alert simulated')).toBeInTheDocument();
  });
});
