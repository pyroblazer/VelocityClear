import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { fetchTransactions } from '../lib/transactionApi';
import { useTransactionStore } from '../stores/transactionStore';
import KPICards from '../components/KPICards';
import TransactionForm from '../components/TransactionForm';
import LiveFeed from '../components/LiveFeed';
import TransactionTable from '../components/TransactionTable';

export default function TransactionsPage() {
  const { setTransactions, setLoading } = useTransactionStore();

  const { data, isLoading, isError } = useQuery({
    queryKey: ['transactions'],
    queryFn: fetchTransactions,
    refetchInterval: 30000,
  });

  useEffect(() => {
    setLoading(isLoading);
    if (data && Array.isArray(data)) setTransactions(data);
  }, [data, isLoading, setTransactions, setLoading]);

  return (
    <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 24 }}>
      <KPICards />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
        <TransactionForm />
        <LiveFeed />
      </div>
      <TransactionTable />
      {isError && (
        <div style={{ padding: '12px 16px', background: '#EF444418', border: '1px solid #EF444440', borderRadius: 8, color: '#EF4444', fontSize: 13, textAlign: 'center' }}>
          Failed to load transactions. The API server may be unavailable.
        </div>
      )}
    </div>
  );
}
