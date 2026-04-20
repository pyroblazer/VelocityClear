import { useMemo } from 'react';
import { useTransactionStore } from '../stores/transactionStore';
import { ArrowRightLeft, DollarSign, TrendingUp, Clock } from 'lucide-react';

interface KPICardProps {
  icon: React.ReactNode;
  label: string;
  value: string;
  accentColor: string;
}

function KPICard({ icon, label, value, accentColor }: KPICardProps) {
  return (
    <div
      style={{
        background: '#1A1A1A',
        borderRadius: 12,
        border: '1px solid #2A2A2A',
        padding: '20px 24px',
        display: 'flex',
        alignItems: 'center',
        gap: 16,
        transition: 'border-color 0.2s',
      }}
      onMouseEnter={(e) => (e.currentTarget.style.borderColor = accentColor)}
      onMouseLeave={(e) => (e.currentTarget.style.borderColor = '#2A2A2A')}
    >
      <div
        style={{
          width: 44,
          height: 44,
          borderRadius: 10,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: `${accentColor}18`,
          flexShrink: 0,
        }}
      >
        {icon}
      </div>
      <div>
        <div style={{ fontSize: 12, color: '#A1A1AA', fontWeight: 600, textTransform: 'uppercase', letterSpacing: 0.05, marginBottom: 4 }}>
          {label}
        </div>
        <div style={{ fontSize: 24, fontWeight: 700, color: '#FFFFFF', fontFamily: 'monospace' }}>
          {value}
        </div>
      </div>
    </div>
  );
}

export default function KPICards() {
  const transactions = useTransactionStore((s) => s.transactions);

  const kpis = useMemo(() => {
    const total = transactions.length;
    const volume = transactions.reduce((sum, t) => sum + t.amount, 0);
    const avg = total > 0 ? volume / total : 0;
    const pending = transactions.filter(
      (t) => t.status.toLowerCase() === 'pending' || t.status.toLowerCase() === 'processing'
    ).length;
    return { total, volume, avg, pending };
  }, [transactions]);

  return (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(4, 1fr)',
        gap: 16,
      }}
    >
      <KPICard
        icon={<ArrowRightLeft size={22} style={{ color: '#3B82F6' }} />}
        label="Total Transactions"
        value={kpis.total.toLocaleString()}
        accentColor="#3B82F6"
      />
      <KPICard
        icon={<DollarSign size={22} style={{ color: '#22C55E' }} />}
        label="Total Volume"
        value={kpis.volume.toLocaleString(undefined, {
          minimumFractionDigits: 2,
          maximumFractionDigits: 2,
        })}
        accentColor="#22C55E"
      />
      <KPICard
        icon={<TrendingUp size={22} style={{ color: '#F59E0B' }} />}
        label="Average Amount"
        value={kpis.avg.toLocaleString(undefined, {
          minimumFractionDigits: 2,
          maximumFractionDigits: 2,
        })}
        accentColor="#F59E0B"
      />
      <KPICard
        icon={<Clock size={22} style={{ color: '#EF4444' }} />}
        label="Pending"
        value={kpis.pending.toLocaleString()}
        accentColor="#EF4444"
      />
    </div>
  );
}
