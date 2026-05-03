import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import TransactionForm from './TransactionForm';

describe('TransactionForm', () => {
  beforeEach(() => { vi.restoreAllMocks(); });

  it('renders in transfer mode by default', () => {
    render(<TransactionForm />);
    expect(screen.getByText('Transfer')).toBeInTheDocument();
    expect(screen.getByText('Pay with Card')).toBeInTheDocument();
    expect(screen.getByTestId('txn-counterparty')).toBeInTheDocument();
  });

  it('shows card fields when card mode selected', () => {
    render(<TransactionForm />);
    fireEvent.click(screen.getByText('Pay with Card'));
    expect(screen.getByTestId('txn-pan')).toBeInTheDocument();
    expect(screen.getByTestId('txn-pin')).toBeInTheDocument();
    expect(screen.getByTestId('txn-card-type')).toBeInTheDocument();
  });

  it('hides counterparty field in card mode', () => {
    render(<TransactionForm />);
    fireEvent.click(screen.getByText('Pay with Card'));
    expect(screen.queryByTestId('txn-counterparty')).not.toBeInTheDocument();
  });

  it('submit button is disabled when required fields are empty', () => {
    render(<TransactionForm />);
    expect(screen.getByTestId('txn-submit')).toBeDisabled();
  });

  it('submit button enables when fields are filled in transfer mode', () => {
    render(<TransactionForm />);
    fireEvent.change(screen.getByTestId('txn-user-id'), { target: { value: 'admin' } });
    fireEvent.change(screen.getByTestId('txn-amount'), { target: { value: '100' } });
    expect(screen.getByTestId('txn-submit')).not.toBeDisabled();
  });

  it('submit button enables when card fields are filled', () => {
    render(<TransactionForm />);
    fireEvent.click(screen.getByText('Pay with Card'));
    fireEvent.change(screen.getByTestId('txn-user-id'), { target: { value: 'admin' } });
    fireEvent.change(screen.getByTestId('txn-amount'), { target: { value: '100' } });
    fireEvent.change(screen.getByTestId('txn-pan'), { target: { value: '4111 1111 1111 1111' } });
    fireEvent.change(screen.getByTestId('txn-pin'), { target: { value: '1234' } });
    expect(screen.getByTestId('txn-submit')).not.toBeDisabled();
  });
});
