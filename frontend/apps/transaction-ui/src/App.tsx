import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { fetchTransactions } from './lib/api';
import { useTransactionStore } from './stores/transactionStore';
import KPICards from './components/KPICards';
import TransactionForm from './components/TransactionForm';
import LiveFeed from './components/LiveFeed';
import TransactionTable from './components/TransactionTable';
import { Zap } from 'lucide-react';

export default function App() {
  const { setTransactions, setLoading } = useTransactionStore();

  const { data, isLoading, isError } = useQuery({
    queryKey: ['transactions'],
    queryFn: fetchTransactions,
    refetchInterval: 30000,
  });

  useEffect(() => {
    setLoading(isLoading);
    if (data && Array.isArray(data)) {
      setTransactions(data);
    }
  }, [data, isLoading, setTransactions, setLoading]);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      {/* Header */}
      <header
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '16px 32px',
          borderBottom: '1px solid #2A2A2A',
          background: '#141414',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <Zap size={24} style={{ color: '#3B82F6' }} />
          <h1 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: '#FFFFFF' }}>
            Transaction Dashboard
          </h1>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, color: '#A1A1AA' }}>
          <span
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: '#22C55E',
              animation: 'pulse-dot 2s infinite',
            }}
          />
          Live
        </div>
      </header>

      {/* Main Content */}
      <main style={{ flex: 1, padding: '24px 32px', display: 'flex', flexDirection: 'column', gap: 24 }}>
        {/* KPI Cards */}
        <KPICards />

        {/* Two-column: Form + Live Feed */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
          <TransactionForm />
          <LiveFeed />
        </div>

        {/* Transaction Table */}
        <TransactionTable />

        {/* Error state */}
        {isError && (
          <div
            style={{
              padding: '12px 16px',
              background: '#EF444418',
              border: '1px solid #EF444440',
              borderRadius: 8,
              color: '#EF4444',
              fontSize: 13,
              textAlign: 'center',
            }}
          >
            Failed to load transactions. The API server may be unavailable.
          </div>
        )}
      </main>

      {/* Footer */}
      <footer
        style={{
          padding: '16px 32px',
          borderTop: '1px solid #2A2A2A',
          background: '#141414',
          textAlign: 'center',
          fontSize: 12,
          color: '#666666',
        }}
      >
        Transaction Dashboard &mdash; Real-time Financial Transaction Platform
      </footer>

      <style>{`
        @keyframes pulse-dot {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.4; }
        }
      `}</style>
    </div>
  );
}
