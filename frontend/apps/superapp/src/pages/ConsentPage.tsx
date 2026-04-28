import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { FileCheck, Plus, Minus } from 'lucide-react';
import { consentApi } from '../lib/complianceApi';

const CONSENT_TYPES = ['DataProcessing', 'Marketing', 'ThirdPartySharing', 'CrossBorderTransfer', 'DataDeletion'];
const card = { background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 };
const input = { background: '#0A0A0A', border: '1px solid #3A3A3A', borderRadius: 8, padding: '8px 12px', color: '#FFF', fontSize: 13, width: '100%', boxSizing: 'border-box' as const };
const btn = (color = '#3B82F6') => ({ background: color, border: 'none', borderRadius: 8, padding: '9px 16px', color: '#FFF', fontSize: 13, fontWeight: 600, cursor: 'pointer' });

export default function ConsentPage() {
  const qc = useQueryClient();
  const [userId, setUserId] = useState('');
  const [lookupId, setLookupId] = useState('');
  const [selectedType, setSelectedType] = useState(CONSENT_TYPES[0]);

  const { data: consents } = useQuery({
    queryKey: ['consent', lookupId],
    queryFn: () => consentApi.listByUser(lookupId),
    enabled: !!lookupId,
  });

  const grantMut = useMutation({
    mutationFn: () => consentApi.grant({ userId, consentType: selectedType, ipAddress: '127.0.0.1', legalBasis: 'POJK No.6/POJK.07/2022' }),
    onSuccess: () => { setLookupId(userId); qc.invalidateQueries({ queryKey: ['consent'] }); },
  });

  const withdrawMut = useMutation({
    mutationFn: (type: string) => consentApi.withdraw({ userId: lookupId, consentType: type, reason: 'User request' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['consent'] }),
  });

  const statusColor = (s: string) => ({ Granted: '#22C55E', Withdrawn: '#EF4444', Expired: '#A1A1AA' }[s] ?? '#A1A1AA');

  return (
    <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20, maxWidth: 900 }}>
      <h1 style={{ color: '#FFF', fontSize: 22, fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
        <FileCheck size={20} style={{ color: '#22C55E' }} /> Consent Management
      </h1>

      <div style={card}>
        <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Grant Consent</h2>
        <div style={{ display: 'flex', gap: 10, marginBottom: 12 }}>
          <input style={input} placeholder="User ID" value={userId} onChange={e => setUserId(e.target.value)} />
          <select style={{ ...input, flex: 0.5 }} value={selectedType} onChange={e => setSelectedType(e.target.value)}>
            {CONSENT_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
          </select>
        </div>
        <button style={btn()} disabled={!userId} onClick={() => grantMut.mutate()}>
          <Plus size={13} style={{ marginRight: 6 }} />
          {grantMut.isPending ? 'Granting…' : 'Grant Consent'}
        </button>
        {grantMut.isSuccess && <p style={{ color: '#22C55E', fontSize: 12, marginTop: 8 }}>Consent granted. Look up below.</p>}
      </div>

      <div style={card}>
        <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Consent Records</h2>
        <input style={input} placeholder="User ID to look up" value={lookupId} onChange={e => setLookupId(e.target.value)} />

        {lookupId && Array.isArray(consents) && consents.length === 0 && (
          <p style={{ color: '#A1A1AA', fontSize: 12, marginTop: 12 }}>No consent records.</p>
        )}

        {Array.isArray(consents) && consents.map((c) => (
          <div key={c.id} style={{ marginTop: 12, padding: 12, background: '#0A0A0A', borderRadius: 8, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <span style={{ color: '#FFF', fontSize: 13, fontWeight: 600 }}>{c.consentType}</span>
              <span style={{ color: statusColor(c.status), fontSize: 11, marginLeft: 10, background: `${statusColor(c.status)}18`, padding: '2px 8px', borderRadius: 10 }}>{c.status}</span>
              <div style={{ color: '#A1A1AA', fontSize: 11, marginTop: 3 }}>Granted: {new Date(c.grantedAt).toLocaleDateString()}</div>
            </div>
            {c.status === 'Granted' && (
              <button style={btn('#EF4444')} onClick={() => withdrawMut.mutate(c.consentType)}>
                <Minus size={12} style={{ marginRight: 4 }} />Withdraw
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
