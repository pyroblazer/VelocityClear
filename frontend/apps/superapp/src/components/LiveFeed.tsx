import { useState, useCallback } from 'react';
import { useSSE } from '../hooks/useSSE';
import { useTransactionStore } from '../stores/transactionStore';
import { Radio, ArrowUpRight, ArrowDownLeft } from 'lucide-react';

interface LiveEvent {
  id: string;
  type: string;
  message: string;
  timestamp: string;
}

export default function LiveFeed() {
  const [events, setEvents] = useState<LiveEvent[]>([]);
  const addTransaction = useTransactionStore((s) => s.addTransaction);

  const handleMessage = useCallback(
    (data: LiveEvent) => {
      setEvents((prev) => [data, ...prev].slice(0, 50));
      if (data.type === 'transaction' && (data as unknown as Record<string, unknown>).transaction) {
        addTransaction((data as unknown as Record<string, unknown>).transaction as Parameters<typeof addTransaction>[0]);
      }
    },
    [addTransaction]
  );

  useSSE<LiveEvent>('/api/transactions/stream', handleMessage);

  return (
    <div style={{ background: '#1A1A1A', borderRadius: 12, border: '1px solid #2A2A2A', display: 'flex', flexDirection: 'column', height: '100%', minHeight: 400 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '16px 20px', borderBottom: '1px solid #2A2A2A', flexShrink: 0 }}>
        <Radio size={20} style={{ color: '#22C55E' }} />
        <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: '#FFFFFF' }}>Live Feed</h2>
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, marginLeft: 'auto', fontSize: 12, color: '#22C55E', fontWeight: 600 }}>
          <span style={{ width: 8, height: 8, borderRadius: '50%', background: '#22C55E', animation: 'pulse-dot 2s infinite' }} />
          LIVE
        </span>
      </div>
      <div style={{ flex: 1, overflowY: 'auto', padding: 12, display: 'flex', flexDirection: 'column', gap: 8 }}>
        {events.length === 0 && (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', color: '#A1A1AA', fontSize: 13, textAlign: 'center', padding: 40 }}>
            Waiting for real-time events...
          </div>
        )}
        {events.map((evt, idx) => (
          <div key={evt.id || idx} style={{ display: 'flex', alignItems: 'flex-start', gap: 10, padding: '10px 12px', background: '#141414', borderRadius: 8, border: '1px solid #2A2A2A', fontSize: 13 }}>
            <div style={{ width: 28, height: 28, borderRadius: 6, display: 'flex', alignItems: 'center', justifyContent: 'center', background: evt.type === 'created' || evt.type === 'transaction' ? '#3B82F618' : evt.type === 'completed' || evt.type === 'success' ? '#22C55E18' : evt.type === 'failed' || evt.type === 'error' ? '#EF444418' : '#F59E0B18', flexShrink: 0 }}>
              {evt.type === 'created' || evt.type === 'transaction' ? <ArrowUpRight size={14} style={{ color: '#3B82F6' }} /> : evt.type === 'completed' || evt.type === 'success' ? <ArrowDownLeft size={14} style={{ color: '#22C55E' }} /> : <ArrowUpRight size={14} style={{ color: '#F59E0B' }} />}
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ color: '#FFFFFF', lineHeight: 1.4 }}>{evt.message}</div>
              <div style={{ color: '#666666', fontSize: 11, marginTop: 4 }}>{evt.timestamp ? new Date(evt.timestamp).toLocaleTimeString() : ''}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
