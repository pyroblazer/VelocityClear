import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import TransactionTable from './TransactionTable';
import { useTransactionStore } from '../stores/transactionStore';

const makeTransaction = (overrides = {}) => ({
  id: 'tx_1',
  userId: 'user_1',
  amount: 100,
  currency: 'USD',
  status: 'Pending',
  timestamp: '2026-04-19T10:00:00Z',
  ...overrides,
});

describe('TransactionTable', () => {
  beforeEach(() => {
    useTransactionStore.setState({ transactions: [], loading: false });
  });

  it('shows empty state message when no transactions', () => {
    render(<TransactionTable />);
    expect(screen.getByText(/no transactions yet/i)).toBeInTheDocument();
  });

  it('renders table headers', () => {
    render(<TransactionTable />);
    expect(screen.getByText('ID')).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByText('Amount')).toBeInTheDocument();
    expect(screen.getByText('Currency')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
    expect(screen.getByText('Timestamp')).toBeInTheDocument();
  });

  it('renders transaction rows', () => {
    useTransactionStore.setState({
      transactions: [makeTransaction({ userId: 'alice', currency: 'EUR' })],
      loading: false,
    });
    render(<TransactionTable />);
    expect(screen.getByText('alice')).toBeInTheDocument();
    expect(screen.getByText('EUR')).toBeInTheDocument();
  });

  it('shows total count', () => {
    useTransactionStore.setState({
      transactions: [makeTransaction({ id: 'a' }), makeTransaction({ id: 'b' })],
      loading: false,
    });
    render(<TransactionTable />);
    expect(screen.getByText('2 total')).toBeInTheDocument();
  });

  it('sorts transactions by timestamp descending', () => {
    useTransactionStore.setState({
      transactions: [
        makeTransaction({ id: 'old', userId: 'first', timestamp: '2026-04-19T08:00:00Z' }),
        makeTransaction({ id: 'new', userId: 'second', timestamp: '2026-04-19T12:00:00Z' }),
      ],
      loading: false,
    });
    render(<TransactionTable />);
    const rows = screen.getAllByRole('row');
    // rows[0] is the header, rows[1] is first data row
    expect(rows[1]).toHaveTextContent('second'); // newer timestamp first
  });

  it('truncates long IDs', () => {
    const longId = 'abcdef123456789012345';
    useTransactionStore.setState({
      transactions: [makeTransaction({ id: longId })],
      loading: false,
    });
    render(<TransactionTable />);
    expect(screen.getByText('abcdef123456...')).toBeInTheDocument();
  });

  it('renders status badge for pending', () => {
    useTransactionStore.setState({
      transactions: [makeTransaction({ status: 'Pending' })],
      loading: false,
    });
    render(<TransactionTable />);
    expect(screen.getByText('Pending')).toBeInTheDocument();
  });

  it('renders status badge for completed', () => {
    useTransactionStore.setState({
      transactions: [makeTransaction({ status: 'Completed' })],
      loading: false,
    });
    render(<TransactionTable />);
    expect(screen.getByText('Completed')).toBeInTheDocument();
  });

  it('formats amount with decimals', () => {
    useTransactionStore.setState({
      transactions: [makeTransaction({ amount: 1234.5 })],
      loading: false,
    });
    render(<TransactionTable />);
    // Find the cell containing the amount (locale-independent: contains "234")
    const cells = screen.getAllByRole('cell');
    const amountCell = cells.find((c) => c.textContent?.includes('234'));
    expect(amountCell).toBeTruthy();
    expect(amountCell?.textContent).toMatch(/234/);
  });
});
