import { create } from 'zustand';

interface Transaction {
  id: string;
  userId: string;
  amount: number;
  currency: string;
  status: string;
  timestamp: string;
  description?: string;
  counterparty?: string;
}

interface TransactionStore {
  transactions: Transaction[];
  loading: boolean;
  setTransactions: (txns: Transaction[]) => void;
  addTransaction: (txn: Transaction) => void;
  setLoading: (loading: boolean) => void;
}

export const useTransactionStore = create<TransactionStore>((set) => ({
  transactions: [],
  loading: false,
  setTransactions: (transactions) => set({ transactions }),
  addTransaction: (txn) => set((s) => ({ transactions: [txn, ...s.transactions] })),
  setLoading: (loading) => set({ loading }),
}));
