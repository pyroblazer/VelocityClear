import { useState } from 'react';
import { useCardStore } from '../stores/cardStore';
import { generateKey } from '../lib/cardApi';
import { Key, RefreshCw } from 'lucide-react';

const KEY_TYPES = ['ZPK', 'ZMK', 'PVK', 'CVK', 'TAK'];

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
  const { keys, addKey } = useCardStore();
  const [keyType, setKeyType] = useState('ZPK');
  const [keyId, setKeyId] = useState('');
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const handleGenerate = async () => {
    if (!keyId.trim()) return;
    setLoading(true);
    setMessage(null);
    try {
      const result = await generateKey(keyType, keyId.trim());
      addKey(result.keyId);
      setMessage({ text: `Generated ${keyType} key: ${result.keyId} (KCV: ${result.keyCheckValue})`, ok: true });
      setKeyId('');
    } catch (err: unknown) {
      setMessage({ text: err instanceof Error ? err.message : 'Failed to generate key', ok: false });
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

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 16 }}>
        <select value={keyType} onChange={(e) => setKeyType(e.target.value)} style={inputStyle} data-testid="key-type-select">
          {KEY_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
        <div style={{ fontSize: 11, color: '#666666', lineHeight: 1.4 }}>
          {keyType === 'ZPK' && 'ZPK (Zone PIN Key) - Scrambles your PIN so it travels safely, like a locked briefcase.'}
          {keyType === 'ZMK' && 'ZMK (Zone Master Key) - The key that protects other keys during shipping, like a tamper-proof container.'}
          {keyType === 'PVK' && 'PVK (PIN Verification Key) - Checks if a PIN is correct using a fingerprint, without storing the real PIN.'}
          {keyType === 'CVK' && 'CVK (Card Verification Key) - Generates the 3-digit CVV on the back of your card.'}
          {keyType === 'TAK' && 'TAK (Terminal Authentication Key) - A secret handshake between the ATM and the bank.'}
        </div>
        <input
          placeholder="Key ID (e.g. my-zpk-001)"
          value={keyId}
          onChange={(e) => setKeyId(e.target.value)}
          style={inputStyle}
          data-testid="key-id-input"
        />
        <button
          onClick={handleGenerate}
          disabled={!keyId.trim() || loading}
          data-testid="generate-key-btn"
          style={{ background: keyId.trim() && !loading ? '#3B82F6' : '#2A2A2A', color: keyId.trim() && !loading ? '#FFFFFF' : '#666666', border: 'none', borderRadius: 6, padding: '8px 16px', cursor: keyId.trim() && !loading ? 'pointer' : 'not-allowed', fontSize: 13, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6 }}
        >
          {loading ? <RefreshCw size={14} style={{ animation: 'spin 1s linear infinite' }} /> : <Key size={14} />}
          {loading ? 'Generating...' : 'Generate Key'}
        </button>
      </div>

      {message && (
        <div style={{ padding: '8px 12px', borderRadius: 4, fontSize: 12, marginBottom: 12, background: message.ok ? '#22C55E18' : '#EF444418', border: `1px solid ${message.ok ? '#22C55E40' : '#EF444440'}`, color: message.ok ? '#22C55E' : '#EF4444', wordBreak: 'break-all' }}>
          {message.text}
        </div>
      )}

      <div>
        <div style={{ fontSize: 12, color: '#A1A1AA', marginBottom: 8 }}>Active Keys</div>
        {keys.length === 0 ? (
          <div style={{ color: '#666666', fontSize: 12 }}>No keys loaded</div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            {keys.map((k) => (
              <div key={k} style={{ fontFamily: 'monospace', fontSize: 12, color: '#3B82F6', background: '#0A0A0A', padding: '4px 8px', borderRadius: 4 }}>
                {k}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
