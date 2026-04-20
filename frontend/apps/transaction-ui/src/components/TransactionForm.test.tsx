import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TransactionForm from './TransactionForm';
import { useTransactionStore } from '../stores/transactionStore';
import * as api from '../lib/api';

vi.mock('../lib/api');

describe('TransactionForm', () => {
  beforeEach(() => {
    useTransactionStore.setState({ transactions: [], loading: false });
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders form fields', () => {
    render(<TransactionForm />);
    expect(screen.getByPlaceholderText(/enter user id/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText('0.00')).toBeInTheDocument();
    expect(screen.getByText(/submit transaction/i)).toBeInTheDocument();
  });

  it('submit button is disabled when fields are empty', () => {
    render(<TransactionForm />);
    expect(screen.getByText(/submit transaction/i).closest('button')).toBeDisabled();
  });

  it('submit button is disabled when amount is zero', async () => {
    render(<TransactionForm />);
    await userEvent.type(screen.getByPlaceholderText(/enter user id/i), 'user1');
    await userEvent.type(screen.getByPlaceholderText('0.00'), '0');
    expect(screen.getByText(/submit transaction/i).closest('button')).toBeDisabled();
  });

  it('submit button is enabled when userId and positive amount are set', async () => {
    render(<TransactionForm />);
    await userEvent.type(screen.getByPlaceholderText(/enter user id/i), 'user1');
    await userEvent.type(screen.getByPlaceholderText('0.00'), '100');
    expect(screen.getByText(/submit transaction/i).closest('button')).not.toBeDisabled();
  });

  it('calls createTransaction with correct payload on submit', async () => {
    vi.mocked(api.createTransaction).mockResolvedValueOnce({ id: 'tx_new', status: 'Pending' });

    render(<TransactionForm />);
    await userEvent.type(screen.getByPlaceholderText(/enter user id/i), 'user_test');
    await userEvent.type(screen.getByPlaceholderText('0.00'), '250');
    await userEvent.click(screen.getByText(/submit transaction/i).closest('button')!);

    expect(api.createTransaction).toHaveBeenCalledWith(
      expect.objectContaining({
        userId: 'user_test',
        amount: 250,
        currency: 'USD',
      })
    );
  });

  it('shows success message after successful submission', async () => {
    vi.mocked(api.createTransaction).mockResolvedValueOnce({ id: 'tx_new', status: 'Pending' });

    render(<TransactionForm />);
    await userEvent.type(screen.getByPlaceholderText(/enter user id/i), 'user1');
    await userEvent.type(screen.getByPlaceholderText('0.00'), '100');
    await userEvent.click(screen.getByText(/submit transaction/i).closest('button')!);

    await waitFor(() => {
      expect(screen.getByText(/transaction submitted successfully/i)).toBeInTheDocument();
    });
  });

  it('shows error message when API returns error', async () => {
    vi.mocked(api.createTransaction).mockResolvedValueOnce({ error: 'Amount must be > 0' });

    render(<TransactionForm />);
    await userEvent.type(screen.getByPlaceholderText(/enter user id/i), 'user1');
    await userEvent.type(screen.getByPlaceholderText('0.00'), '100');
    await userEvent.click(screen.getByText(/submit transaction/i).closest('button')!);

    await waitFor(() => {
      expect(screen.getByText('Amount must be > 0')).toBeInTheDocument();
    });
  });

  it('shows network error message on exception', async () => {
    vi.mocked(api.createTransaction).mockRejectedValueOnce(new Error('Network error'));

    render(<TransactionForm />);
    await userEvent.type(screen.getByPlaceholderText(/enter user id/i), 'user1');
    await userEvent.type(screen.getByPlaceholderText('0.00'), '100');
    await userEvent.click(screen.getByText(/submit transaction/i).closest('button')!);

    await waitFor(() => {
      expect(screen.getByText(/network error/i)).toBeInTheDocument();
    });
  });

  it('resets form fields after successful submission', async () => {
    vi.mocked(api.createTransaction).mockResolvedValueOnce({ id: 'tx_new', status: 'Pending' });

    render(<TransactionForm />);
    const userIdInput = screen.getByPlaceholderText(/enter user id/i) as HTMLInputElement;
    const amountInput = screen.getByPlaceholderText('0.00') as HTMLInputElement;

    await userEvent.type(userIdInput, 'user1');
    await userEvent.type(amountInput, '100');
    await userEvent.click(screen.getByText(/submit transaction/i).closest('button')!);

    await waitFor(() => {
      expect(userIdInput.value).toBe('');
      expect(amountInput.value).toBe('');
    });
  });

  it('renders currency selector with USD default', () => {
    render(<TransactionForm />);
    const select = screen.getByRole('combobox') as HTMLSelectElement;
    expect(select.value).toBe('USD');
  });

  it('renders optional description and counterparty fields', () => {
    render(<TransactionForm />);
    expect(screen.getByPlaceholderText(/optional description/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/optional counterparty/i)).toBeInTheDocument();
  });
});
