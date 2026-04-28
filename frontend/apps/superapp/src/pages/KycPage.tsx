import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Shield, Eye, Search } from 'lucide-react';
import type { KycProfileResponse } from '../lib/complianceApi';
import { kycApi } from '../lib/complianceApi';

const card = { background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 };
const input = { background: '#0A0A0A', border: '1px solid #3A3A3A', borderRadius: 8, padding: '8px 12px', color: '#FFF', fontSize: 13, width: '100%', boxSizing: 'border-box' as const };
const btn = (color = '#3B82F6') => ({ background: color, border: 'none', borderRadius: 8, padding: '9px 16px', color: '#FFF', fontSize: 13, fontWeight: 600, cursor: 'pointer' });

export default function KycPage() {
  const qc = useQueryClient();
  const [userId, setUserId] = useState('');
  const [fullName, setFullName] = useState('');
  const [idNumber, setIdNumber] = useState('');
  const [lookupId, setLookupId] = useState('');

  const { data: profile, isLoading } = useQuery({
    queryKey: ['kyc', lookupId],
    queryFn: () => kycApi.getByUser(lookupId),
    enabled: !!lookupId,
  });

  const initiateMut = useMutation({
    mutationFn: () => kycApi.initiate({ userId, fullName, idNumber, idType: 'KTP' }),
    onSuccess: () => { setLookupId(userId); qc.invalidateQueries({ queryKey: ['kyc'] }); },
  });

  const livenessMut = useMutation({
    mutationFn: (kycProfileId: string) => kycApi.liveness(kycProfileId, lookupId),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['kyc'] }),
  });

  const p = profile as KycProfileResponse | undefined;

  const screenMut = useMutation({
    mutationFn: (kycProfileId: string) => kycApi.screen(kycProfileId, { kycProfileId, fullName: p?.fullName ?? '', idNumber: null }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['kyc'] }),
  });

  const statusColor = (s: string) => ({ Verified: '#22C55E', Rejected: '#EF4444', InProgress: '#F59E0B', Pending: '#A1A1AA' }[s] ?? '#A1A1AA');

  return (
    <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20, maxWidth: 900 }}>
      <h1 style={{ color: '#FFF', fontSize: 22, fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
        <Shield size={20} style={{ color: '#3B82F6' }} /> KYC Management
      </h1>

      {/* Initiate KYC */}
      <div style={card}>
        <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Initiate KYC</h2>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12, marginBottom: 12 }}>
          <input style={input} placeholder="User ID" value={userId} onChange={e => setUserId(e.target.value)} />
          <input style={input} placeholder="Full Name" value={fullName} onChange={e => setFullName(e.target.value)} />
          <input style={input} placeholder="ID Number (KTP)" value={idNumber} onChange={e => setIdNumber(e.target.value)} />
        </div>
        <button style={btn()} disabled={!userId || !fullName || !idNumber} onClick={() => initiateMut.mutate()}>
          {initiateMut.isPending ? 'Initiating…' : 'Initiate KYC'}
        </button>
        {initiateMut.isSuccess && <p style={{ color: '#22C55E', fontSize: 12, marginTop: 8 }}>KYC initiated. Look up profile below.</p>}
      </div>

      {/* Lookup */}
      <div style={card}>
        <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Look Up Profile</h2>
        <div style={{ display: 'flex', gap: 10 }}>
          <input style={{ ...input, flex: 1 }} placeholder="User ID" value={lookupId} onChange={e => setLookupId(e.target.value)} />
          <button style={{ ...btn('#27272A'), display: 'flex', alignItems: 'center', gap: 6 }}>
            <Search size={13} /> Search
          </button>
        </div>
      </div>

      {/* Profile result */}
      {!!lookupId && !isLoading && p != null && (
        <div style={card}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 16 }}>
            <h2 style={{ color: '#FFF', fontSize: 15, margin: 0 }}>Profile: {p.userId}</h2>
            <span style={{ color: statusColor(p.status), fontSize: 12, fontWeight: 600, background: `${statusColor(p.status)}18`, padding: '4px 10px', borderRadius: 20 }}>
              {p.status}
            </span>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 16 }}>
            <div style={{ fontSize: 12, color: '#A1A1AA' }}>
              <div>Liveness Checked: <span style={{ color: p.livenessChecked ? '#22C55E' : '#EF4444' }}>{p.livenessChecked ? '✓' : '✗'}</span></div>
              <div>Liveness Confidence: <span style={{ color: '#FFF' }}>{((p.livenessConfidence ?? 0) * 100).toFixed(1)}%</span></div>
            </div>
            <div style={{ fontSize: 12, color: '#A1A1AA' }}>
              <div>Watchlist Screened: <span style={{ color: p.watchlistScreened ? '#22C55E' : '#F59E0B' }}>{p.watchlistScreened ? '✓' : 'Pending'}</span></div>
              <div>Watchlist Hit: <span style={{ color: p.watchlistHit ? '#EF4444' : '#22C55E' }}>{p.watchlistHit ? 'HIT' : 'Clean'}</span></div>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 10 }}>
            <button style={btn('#F59E0B')} onClick={() => livenessMut.mutate(p.id)}>
              <Eye size={13} style={{ marginRight: 6 }} />
              {livenessMut.isPending ? 'Checking…' : 'Run Liveness Check'}
            </button>
            <button style={btn('#8B5CF6')} onClick={() => screenMut.mutate(p.id)}>
              <Search size={13} style={{ marginRight: 6 }} />
              {screenMut.isPending ? 'Screening…' : 'Screen Watchlist'}
            </button>
          </div>
        </div>
      )}

      {!!lookupId && !isLoading && p == null && (
        <div style={{ ...card, color: '#A1A1AA', fontSize: 13 }}>No KYC profile found for user &quot;{lookupId}&quot;.</div>
      )}
    </div>
  );
}
