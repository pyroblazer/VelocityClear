import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import SettingsPage from './SettingsPage';

vi.mock('../lib/apiKeyApi', () => ({
  apiKeyApi: {
    list: vi.fn(),
    create: vi.fn(),
    revoke: vi.fn(),
  },
}));

import { apiKeyApi } from '../lib/apiKeyApi';

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <SettingsPage />
    </QueryClientProvider>,
  );
}

const mockKeys = [
  {
    id: 'key-1',
    name: 'Production',
    keyPrefix: 'vc_live_a1b2',
    permissions: ['transactions:read', 'transactions:write'],
    createdAt: '2026-01-15T10:00:00Z',
    lastUsedAt: '2026-01-20T08:30:00Z',
    isActive: true,
  },
  {
    id: 'key-2',
    name: 'Staging',
    keyPrefix: 'vc_live_c3d4',
    permissions: ['audit:read'],
    createdAt: '2026-02-01T12:00:00Z',
    lastUsedAt: null,
    isActive: false,
  },
];

describe('SettingsPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.mocked(apiKeyApi.list).mockResolvedValue(mockKeys);
  });

  it('renders settings header', async () => {
    renderPage();
    expect(screen.getByText('Settings')).toBeInTheDocument();
    expect(await screen.findByText('Production')).toBeInTheDocument();
  });

  it('lists API keys from API', async () => {
    renderPage();
    expect(await screen.findByText('Staging')).toBeInTheDocument();
    expect(screen.getByText('vc_live_a1b2...')).toBeInTheDocument();
    expect(screen.getByText('vc_live_c3d4...')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Revoked')).toBeInTheDocument();
  });

  it('shows create form on Generate New Key click', async () => {
    renderPage();
    expect(await screen.findByText('Generate New Key')).toBeInTheDocument();
    fireEvent.click(screen.getByText('Generate New Key'));
    expect(screen.getByText('Generate New API Key')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('e.g., Production Integration')).toBeInTheDocument();
  });

  it('generates key and shows it once', async () => {
    const mockResponse = {
      id: 'key-3',
      name: 'Test Key',
      keyPrefix: 'vc_live_e5f6',
      permissions: ['transactions:read'],
      createdAt: '2026-05-03T10:00:00Z',
      lastUsedAt: null,
      isActive: true,
      apiKey: 'vc_live_e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0',
    };
    vi.mocked(apiKeyApi.create).mockResolvedValue(mockResponse);

    renderPage();
    fireEvent.click(await screen.findByText('Generate New Key'));

    const nameInput = screen.getByPlaceholderText('e.g., Production Integration');
    fireEvent.change(nameInput, { target: { value: 'Test Key' } });
    fireEvent.click(screen.getByText('Generate Key'));

    await waitFor(() => {
      expect(apiKeyApi.create).toHaveBeenCalledWith('Test Key', expect.any(Array));
    });

    expect(await screen.findByText('API Key Generated')).toBeInTheDocument();
    expect(screen.getByText('vc_live_e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0')).toBeInTheDocument();
    expect(screen.getByText(/This key will only be shown once/)).toBeInTheDocument();
  });

  it('revokes key after confirmation', async () => {
    vi.mocked(apiKeyApi.revoke).mockResolvedValue(undefined);
    renderPage();

    const revokeButtons = await screen.findAllByTitle('Revoke key');
    fireEvent.click(revokeButtons[0]);
    expect(screen.getByText('Confirm?')).toBeInTheDocument();

    fireEvent.click(screen.getByText('Yes'));
    await waitFor(() => {
      expect(apiKeyApi.revoke).toHaveBeenCalledWith('key-1');
    });
  });

  it('shows empty state when no keys', async () => {
    vi.mocked(apiKeyApi.list).mockResolvedValue([]);
    renderPage();
    expect(await screen.findByText('No API keys yet. Generate one above.')).toBeInTheDocument();
  });
});
