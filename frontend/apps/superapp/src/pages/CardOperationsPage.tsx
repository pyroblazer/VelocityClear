import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Shield } from 'lucide-react';
import { listHsmKeys } from '../lib/cardApi';
import { useCardStore } from '../stores/cardStore';
import HsmKeyManagement from '../components/HsmKeyManagement';
import PinOperations from '../components/PinOperations';
import Iso8583Tools from '../components/Iso8583Tools';

export default function CardOperationsPage() {
  const { setKeys, setError } = useCardStore();

  const { data, isError } = useQuery({
    queryKey: ['hsm-keys'],
    queryFn: listHsmKeys,
    refetchInterval: 30000,
  });

  useEffect(() => {
    if (data?.keyIds) setKeys(data.keyIds);
    if (isError) setError('Failed to connect to HSM service');
  }, [data, isError, setKeys, setError]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column' }}>
      <header
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '16px 24px',
          borderBottom: '1px solid #2A2A2A',
          background: '#141414',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <Shield size={22} style={{ color: '#3B82F6' }} />
          <h1 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: '#FFFFFF' }}>
            Card Operations
          </h1>
        </div>
        <div style={{ fontSize: 13, color: '#A1A1AA' }}>
          ISO 8583 &bull; HSM &bull; PIN Encryption
        </div>
      </header>
      <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 24 }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 24 }}>
          <HsmKeyManagement />
          <PinOperations />
        </div>
        <Iso8583Tools />
      </div>
    </div>
  );
}
