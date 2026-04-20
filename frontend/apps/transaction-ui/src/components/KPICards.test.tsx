import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import KPICards from './KPICards';
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

describe('KPICards', () => {
  beforeEach(() => {
    useTransactionStore.setState({ transactions: [], loading: false });
  });

  it('shows zero totals when no transactions', () => {
    render(<KPICards />);
    // Total count = 0, pending = 0 → at least two "0" values
    const zeros = screen.getAllByText('0');
    expect(zeros.length).toBeGreaterThanOrEqual(2);
  });

  it('shows total transaction count', () => {
    useTransactionStore.setState({
      transactions: [makeTransaction({ id: 'a' }), makeTransaction({ id: 'b' })],
      loading: false,
    });
    render(<KPICards />);
    // Total = 2 and pending = 2 (both Pending status)
    const twos = screen.getAllByText('2');
    expect(twos.length).toBeGreaterThanOrEqual(1);
  });

  it('shows correct total volume', () => {
    useTransactionStore.setState({
      transactions: [
        makeTransaction({ amount: 100 }),
        makeTransaction({ id: 'b', amount: 250 }),
      ],
      loading: false,
    });
    render(<KPICards />);
    // Find the "Total Volume" label and check its card contains "350"
    const label = screen.getByText('Total Volume');
    expect(label.parentElement).toHaveTextContent(/350/);
  });

  it('shows correct average amount', () => {
    useTransactionStore.setState({
      transactions: [
        makeTransaction({ id: 'a', amount: 100 }),
        makeTransaction({ id: 'b', amount: 300 }),
      ],
      loading: false,
    });
    render(<KPICards />);
    const label = screen.getByText('Average Amount');
    expect(label.parentElement).toHaveTextContent(/200/);
  });

  it('counts pending transactions', () => {
    useTransactionStore.setState({
      transactions: [
        makeTransaction({ id: 'a', status: 'Pending' }),
        makeTransaction({ id: 'b', status: 'Completed' }),
        makeTransaction({ id: 'c', status: 'Processing' }),
      ],
      loading: false,
    });
    render(<KPICards />);
    // pending count is 2
    const twos = screen.getAllByText('2');
    expect(twos.length).toBeGreaterThanOrEqual(1);
  });

  it('renders all four KPI labels', () => {
    render(<KPICards />);
    expect(screen.getByText('Total Transactions')).toBeInTheDocument();
    expect(screen.getByText('Total Volume')).toBeInTheDocument();
    expect(screen.getByText('Average Amount')).toBeInTheDocument();
    expect(screen.getByText('Pending')).toBeInTheDocument();
  });

  it('average is zero when no transactions', () => {
    render(<KPICards />);
    // Volume and average cards both should show 0 when empty
    const volumeLabel = screen.getByText('Total Volume');
    const avgLabel = screen.getByText('Average Amount');
    expect(volumeLabel.parentElement).toHaveTextContent(/0/);
    expect(avgLabel.parentElement).toHaveTextContent(/0/);
  });
});
