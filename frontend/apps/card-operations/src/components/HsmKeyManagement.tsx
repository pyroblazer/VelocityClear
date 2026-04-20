import { useState } from 'react';
import { useCardStore } from '../stores/cardStore';
import { generateKey } from '../lib/api';
import { Key, Plus, RefreshCw } from 'lucide-react';

const cardStyle: React.CSSProperties = {
  background: '#1A1A1A',
  borderRadius: 8,
  border: '1px solid #2A2A2A',
  padding: 20,
};

const inputStyle: React.CSSProperties = {
  background: '#0A0A0A',
  border: '1px solid #2A2A2A',
  borderRadius: 6,
  padding: '8px 12px',
  color: '#FFFFFF',
  fontSize: 13,
  width: '100%',
  boxSizing: 'border-box',
};

export default function HsmKeyManagement() {
  const { keys, addKey, error, setError } = useCardStore();
  const [keyId, setKeyId] = useState('');
  const [keyType, setKeyType] = useState('ZPK');
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState('');

  const handleGenerate = async () => {
    if (!keyId.trim()) return;
    setLoading(true);
    setError(null);
    setSuccess('');
    try {
      const result = await generateKey(keyType, keyId);
      addKey(result.keyId);
      setSuccess(`Generated ${result.keyType} key: ${result.keyId} (KCV: ${result.keyCheckValue})`);
      setKeyId('');
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to generate key');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={cardStyle}>
      <h2 style={{ margin: '0 0 16px 0', fontSize: 16, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 8 }}>
        <Key size={18} style={{ color: '#3B82F6' }} />
        HSM Key Management
      </h2>
      <p style={{ margin: '0 0 12px 0', fontSize: 12, color: '#888888', lineHeight: 1.5 }}>
        The HSM (Hardware Security Module) is like a tamper-proof safe that holds special keys for encrypting PINs.
        Each key type has a different job, choose one below to generate it.
      </p>
      <details style={{ marginBottom: 12, fontSize: 11, color: '#666666' }}>
        <summary style={{ cursor: 'pointer', color: '#A1A1AA', fontSize: 11 }}>How key generation works internally</summary>
        <div style={{ marginTop: 6, lineHeight: 1.5, padding: 8, background: '#0A0A0A', borderRadius: 4 }}>
          <strong>ZPK</strong>: A random 256-bit AES key. PIN + PAN are packed into an ISO 9564 Format 0 block (16 hex), then encrypted with AES-256-CBC. The encrypted block is what travels over the network.<br />
          <strong>ZMK</strong>: A key-encrypting-key. Used to encrypt ZPKs for safe transport between systems. The recipient decrypts with their copy of the same ZMK.<br />
          <strong>PVK</strong>: Derives a verification value from PIN + PAN + PVK. The bank stores only the value, never the actual PIN.<br />
          <strong>CVK</strong>: Generates CVV/CVC by combining card number + expiry + service code + CVK through a keyed hash.<br />
          <strong>TAK</strong>: Computes a MAC on each terminal-to-host message to prove authenticity and detect tampering.<br />
          <strong>KCV</strong>: Encrypts a zero-block with the new key and returns the first hex chars. Two parties compare KCVs to confirm they hold the same key.
        </div>
      </details>

      <div style={{ marginBottom: 16 }}>
        <div style={{ fontSize: 12, color: '#A1A1AA', marginBottom: 8 }}>Active Keys ({keys.length})</div>
        {keys.length > 0 ? (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {keys.map((k) => (
              <span
                key={k}
                style={{
                  background: '#3B82F618',
                  border: '1px solid #3B82F640',
                  borderRadius: 4,
                  padding: '4px 10px',
                  fontSize: 12,
                  color: '#3B82F6',
                  fontFamily: 'monospace',
                }}
              >
                {k}
              </span>
            ))}
          </div>
        ) : (
          <div style={{ color: '#666666', fontSize: 12 }}>No keys loaded</div>
        )}
      </div>

      <div style={{ borderTop: '1px solid #2A2A2A', paddingTop: 16 }}>
        <div style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 8 }}>Generate New Key</div>
        <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
          <select
            value={keyType}
            onChange={(e) => setKeyType(e.target.value)}
            style={{ ...inputStyle, width: 100, cursor: 'pointer' }}
            data-testid="key-type-select"
          >
            <option value="ZPK">ZPK</option>
            <option value="ZMK">ZMK</option>
            <option value="PVK">PVK</option>
            <option value="CVK">CVK</option>
            <option value="TAK">TAK</option>
          </select>
          <div style={{ fontSize: 11, color: '#666666', lineHeight: 1.4 }}>
            {keyType === 'ZPK' && 'ZPK (Zone PIN Key) - Scrambles your PIN so it travels safely, like a locked briefcase.'}
            {keyType === 'ZMK' && 'ZMK (Zone Master Key) - The key that protects other keys during shipping, like a tamper-proof container.'}
            {keyType === 'PVK' && 'PVK (PIN Verification Key) - Checks if a PIN is correct using a fingerprint, without storing the real PIN.'}
            {keyType === 'CVK' && 'CVK (Card Verification Key) - Generates the 3-digit CVV on the back of your card.'}
            {keyType === 'TAK' && 'TAK (Terminal Authentication Key) - A secret handshake between the ATM and the bank.'}
          </div>
          <input
            placeholder="Key ID"
            value={keyId}
            onChange={(e) => setKeyId(e.target.value)}
            style={inputStyle}
            data-testid="key-id-input"
          />
        </div>
        <button
          onClick={handleGenerate}
          disabled={!keyId.trim() || loading}
          style={{
            background: keyId.trim() && !loading ? '#3B82F6' : '#2A2A2A',
            color: keyId.trim() && !loading ? '#FFFFFF' : '#666666',
            border: 'none',
            borderRadius: 6,
            padding: '8px 16px',
            cursor: keyId.trim() && !loading ? 'pointer' : 'not-allowed',
            fontSize: 13,
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            width: '100%',
            justifyContent: 'center',
          }}
          data-testid="generate-key-btn"
        >
          {loading ? <RefreshCw size={14} className="spin" /> : <Plus size={14} />}
          {loading ? 'Generating...' : 'Generate Key'}
        </button>
        {success && (
          <div style={{ marginTop: 8, color: '#22C55E', fontSize: 12 }}>{success}</div>
        )}
        {error && (
          <div style={{ marginTop: 8, color: '#EF4444', fontSize: 12 }}>{error}</div>
        )}
      </div>
    </div>
  );
}
