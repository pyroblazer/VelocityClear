import { describe, it, expect, beforeEach } from 'vitest';
import { useTransactionStore } from './transactionStore';

const makeTransaction = (overrides = {}) => ({
  id: 'tx_1',
  userId: 'user_1',
  amount: 100,
  currency: 'USD',
  status: 'Pending',
  timestamp: '2026-04-19T10:00:00Z',
  ...overrides,
});

describe('transactionStore', () => {
  beforeEach(() => {
    useTransactionStore.setState({ transactions: [], loading: false });
  });

  it('starts with empty transactions and loading=false', () => {
    const state = useTransactionStore.getState();
    expect(state.transactions).toEqual([]);
    expect(state.loading).toBe(false);
  });

  it('setTransactions replaces all transactions', () => {
    const txns = [makeTransaction({ id: 'a' }), makeTransaction({ id: 'b' })];
    useTransactionStore.getState().setTransactions(txns);
    expect(useTransactionStore.getState().transactions).toHaveLength(2);
    expect(useTransactionStore.getState().transactions[0].id).toBe('a');
  });

  it('setTransactions overwrites existing data', () => {
    useTransactionStore.getState().setTransactions([makeTransaction({ id: 'old' })]);
    useTransactionStore.getState().setTransactions([makeTransaction({ id: 'new' })]);
    const txns = useTransactionStore.getState().transactions;
    expect(txns).toHaveLength(1);
    expect(txns[0].id).toBe('new');
  });

  it('addTransaction prepends to the list', () => {
    useTransactionStore.getState().setTransactions([makeTransaction({ id: 'existing' })]);
    useTransactionStore.getState().addTransaction(makeTransaction({ id: 'new' }));
    const txns = useTransactionStore.getState().transactions;
    expect(txns).toHaveLength(2);
    expect(txns[0].id).toBe('new');
  });

  it('addTransaction on empty list results in one item', () => {
    useTransactionStore.getState().addTransaction(makeTransaction({ id: 'only' }));
    expect(useTransactionStore.getState().transactions).toHaveLength(1);
  });

  it('setLoading updates loading flag to true', () => {
    useTransactionStore.getState().setLoading(true);
    expect(useTransactionStore.getState().loading).toBe(true);
  });

  it('setLoading updates loading flag to false', () => {
    useTransactionStore.getState().setLoading(true);
    useTransactionStore.getState().setLoading(false);
    expect(useTransactionStore.getState().loading).toBe(false);
  });

  it('setTransactions with empty array clears all', () => {
    useTransactionStore.getState().setTransactions([makeTransaction()]);
    useTransactionStore.getState().setTransactions([]);
    expect(useTransactionStore.getState().transactions).toEqual([]);
  });

  it('multiple addTransactions accumulate correctly', () => {
    useTransactionStore.getState().addTransaction(makeTransaction({ id: 'a' }));
    useTransactionStore.getState().addTransaction(makeTransaction({ id: 'b' }));
    useTransactionStore.getState().addTransaction(makeTransaction({ id: 'c' }));
    expect(useTransactionStore.getState().transactions).toHaveLength(3);
  });
});
