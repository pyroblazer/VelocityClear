import { useMemo } from 'react';
import { useTransactionStore } from '../stores/transactionStore';
import { ArrowUpDown, Hash, User, DollarSign, Coins, Clock, Activity } from 'lucide-react';

const statusColorMap: Record<string, string> = {
  completed: '#22C55E',
  success: '#22C55E',
  pending: '#F59E0B',
  failed: '#EF4444',
  rejected: '#EF4444',
  processing: '#3B82F6',
};

function getStatusColor(status: string): string {
  return statusColorMap[status.toLowerCase()] || '#A1A1AA';
}

export default function TransactionTable() {
  const transactions = useTransactionStore((s) => s.transactions);

  const sorted = useMemo(
    () => [...transactions].sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()),
    [transactions]
  );

  const columns = [
    { label: 'ID', icon: <Hash size={14} /> },
    { label: 'User', icon: <User size={14} /> },
    { label: 'Amount', icon: <DollarSign size={14} /> },
    { label: 'Currency', icon: <Coins size={14} /> },
    { label: 'Status', icon: <ArrowUpDown size={14} /> },
    { label: 'Timestamp', icon: <Clock size={14} /> },
  ];

  return (
    <div style={{ background: '#1A1A1A', borderRadius: 12, border: '1px solid #2A2A2A', overflow: 'hidden' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '16px 20px', borderBottom: '1px solid #2A2A2A' }}>
        <Activity size={20} style={{ color: '#3B82F6' }} />
        <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: '#FFFFFF' }}>Transactions</h2>
        <span style={{ marginLeft: 'auto', fontSize: 13, color: '#A1A1AA' }}>{sorted.length} total</span>
      </div>
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid #2A2A2A' }}>
              {columns.map((col) => (
                <th key={col.label} style={{ padding: '12px 16px', fontSize: 12, fontWeight: 600, color: '#A1A1AA', textAlign: 'left', textTransform: 'uppercase', letterSpacing: 0.05, whiteSpace: 'nowrap' }}>
                  <span style={{ display: 'flex', alignItems: 'center', gap: 6 }}>{col.icon}{col.label}</span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {sorted.length === 0 && (
              <tr>
                <td colSpan={6} style={{ padding: 40, textAlign: 'center', color: '#A1A1AA', fontSize: 14 }}>
                  No transactions yet. Create one to get started.
                </td>
              </tr>
            )}
            {sorted.map((txn) => (
              <tr key={txn.id} style={{ borderBottom: '1px solid #2A2A2A', transition: 'background 0.15s' }} onMouseEnter={(e) => (e.currentTarget.style.background = '#141414')} onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}>
                <td style={{ padding: '12px 16px', fontSize: 13, fontFamily: 'monospace', color: '#A1A1AA', maxWidth: 120, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {txn.id.length > 12 ? txn.id.slice(0, 12) + '...' : txn.id}
                </td>
                <td style={{ padding: '12px 16px', fontSize: 13, color: '#FFFFFF' }}>{txn.userId}</td>
                <td style={{ padding: '12px 16px', fontSize: 13, color: '#FFFFFF', fontFamily: 'monospace', fontWeight: 600 }}>
                  {txn.amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                </td>
                <td style={{ padding: '12px 16px', fontSize: 13, color: '#A1A1AA' }}>{txn.currency}</td>
                <td style={{ padding: '12px 16px' }}>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12, fontWeight: 600, padding: '4px 10px', borderRadius: 9999, background: `${getStatusColor(txn.status)}18`, color: getStatusColor(txn.status), textTransform: 'capitalize' }}>
                    <span style={{ width: 6, height: 6, borderRadius: '50%', background: getStatusColor(txn.status) }} />
                    {txn.status}
                  </span>
                </td>
                <td style={{ padding: '12px 16px', fontSize: 13, color: '#A1A1AA' }}>{new Date(txn.timestamp).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
