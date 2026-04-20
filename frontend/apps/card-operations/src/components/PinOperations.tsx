import { useState } from 'react';
import { useCardStore } from '../stores/cardStore';
import { encryptPin, decryptPin, verifyPin } from '../lib/api';
import { Lock, RefreshCw } from 'lucide-react';

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

const btnStyle = (enabled: boolean): React.CSSProperties => ({
  background: enabled ? '#3B82F6' : '#2A2A2A',
  color: enabled ? '#FFFFFF' : '#666666',
  border: 'none',
  borderRadius: 6,
  padding: '8px 16px',
  cursor: enabled ? 'pointer' : 'not-allowed',
  fontSize: 13,
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 6,
  width: '100%',
});

export default function PinOperations() {
  const { encryptedPinBlock, setEncryptedPinBlock, decryptedPin, setDecryptedPin, verificationResult, setVerificationResult, setError, error } = useCardStore();

  const [pin, setPin] = useState('');
  const [pan, setPan] = useState('');
  const [zpkId] = useState('default-zpk');
  const [loading, setLoading] = useState('');

  const [decryptBlock, setDecryptBlock] = useState('');
  const [decryptPan, setDecryptPan] = useState('');
  const [decryptZpkId] = useState('default-zpk');

  const [verifyBlock, setVerifyBlock] = useState('');
  const [verifyPan, setVerifyPan] = useState('');
  const [verifyZpkId] = useState('default-zpk');
  const [expectedPin, setExpectedPin] = useState('');

  const handleEncrypt = async () => {
    if (!pin || !pan) return;
    setLoading('encrypt');
    setError(null);
    try {
      const result = await encryptPin(pin, pan, zpkId);
      setEncryptedPinBlock(result.encryptedPinBlock);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Encryption failed');
    } finally {
      setLoading('');
    }
  };

  const handleDecrypt = async () => {
    if (!decryptBlock || !decryptPan) return;
    setLoading('decrypt');
    setError(null);
    try {
      const result = await decryptPin(decryptBlock, decryptPan, decryptZpkId);
      setDecryptedPin(result.pin);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Decryption failed');
    } finally {
      setLoading('');
    }
  };

  const handleVerify = async () => {
    if (!verifyBlock || !verifyPan || !expectedPin) return;
    setLoading('verify');
    setError(null);
    try {
      const result = await verifyPin(verifyBlock, verifyPan, verifyZpkId, expectedPin);
      setVerificationResult(result);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Verification failed');
    } finally {
      setLoading('');
    }
  };

  return (
    <div style={cardStyle}>
      <h2 style={{ margin: '0 0 16px 0', fontSize: 16, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 8 }}>
        <Lock size={18} style={{ color: '#22C55E' }} />
        PIN Operations
      </h2>
      <p style={{ margin: '0 0 12px 0', fontSize: 12, color: '#888888', lineHeight: 1.5 }}>
        Your PIN should never travel as plain text, that's like mailing your house key in a clear envelope.
        These tools encrypt (lock), decrypt (unlock), and verify PINs using the keys stored in the HSM.
      </p>
      <details style={{ marginBottom: 12, fontSize: 11, color: '#666666' }}>
        <summary style={{ cursor: 'pointer', color: '#A1A1AA', fontSize: 11 }}>How PIN encryption works internally</summary>
        <div style={{ marginTop: 6, lineHeight: 1.5, padding: 8, background: '#0A0A0A', borderRadius: 4 }}>
          <strong>Encrypt</strong>: The PIN digits are XORed with a block derived from the PAN (ISO 9564 Format 0), producing a 64-bit cleartext PIN block. This is then encrypted with AES-256-CBC using the ZPK stored in the HSM. The output is a 16-character hex string.<br />
          <strong>Decrypt</strong>: The reverse. The encrypted PIN block is decrypted with the same ZPK, then the PAN-derived block is XORed back out to recover the original PIN digits.<br />
          <strong>Verify</strong>: Decrypts the PIN block (same as above), then compares the recovered PIN against the expected value you provide. Returns verified/mismatch without ever storing the PIN.
        </div>
      </details>

      {/* Encrypt */}
      <div style={{ marginBottom: 16, paddingBottom: 16, borderBottom: '1px solid #2A2A2A' }}>
        <div style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 8 }}>Encrypt PIN</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <input placeholder="PIN (e.g. 1234)" value={pin} onChange={(e) => setPin(e.target.value)} style={inputStyle} data-testid="encrypt-pin-input" />
          <input placeholder="PAN (e.g. 4111111111111111)" value={pan} onChange={(e) => setPan(e.target.value)} style={inputStyle} data-testid="encrypt-pan-input" />
          <button onClick={handleEncrypt} disabled={!pin || !pan || !!loading} style={btnStyle(!!pin && !!pan && !loading)} data-testid="encrypt-btn">
            {loading === 'encrypt' ? <RefreshCw size={14} className="spin" /> : <Lock size={14} />}
            {loading === 'encrypt' ? 'Encrypting...' : 'Encrypt PIN'}
          </button>
          {encryptedPinBlock && (
            <div style={{ marginTop: 4, background: '#0A0A0A', padding: 8, borderRadius: 4, fontFamily: 'monospace', fontSize: 12, color: '#22C55E', wordBreak: 'break-all' }}>
              {encryptedPinBlock}
            </div>
          )}
        </div>
      </div>

      {/* Decrypt */}
      <div style={{ marginBottom: 16, paddingBottom: 16, borderBottom: '1px solid #2A2A2A' }}>
        <div style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 8 }}>Decrypt PIN Block</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <input placeholder="Encrypted PIN block (hex)" value={decryptBlock} onChange={(e) => setDecryptBlock(e.target.value)} style={inputStyle} data-testid="decrypt-block-input" />
          <input placeholder="PAN" value={decryptPan} onChange={(e) => setDecryptPan(e.target.value)} style={inputStyle} data-testid="decrypt-pan-input" />
          <button onClick={handleDecrypt} disabled={!decryptBlock || !decryptPan || !!loading} style={btnStyle(!!decryptBlock && !!decryptPan && !loading)} data-testid="decrypt-btn">
            {loading === 'decrypt' ? <RefreshCw size={14} className="spin" /> : 'Decrypt PIN Block'}
          </button>
          {decryptedPin && (
            <div style={{ marginTop: 4, background: '#0A0A0A', padding: 8, borderRadius: 4, fontFamily: 'monospace', fontSize: 12, color: '#22C55E' }}>
              PIN: {decryptedPin}
            </div>
          )}
        </div>
      </div>

      {/* Verify */}
      <div>
        <div style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 8 }}>Verify PIN</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <input placeholder="Encrypted PIN block" value={verifyBlock} onChange={(e) => setVerifyBlock(e.target.value)} style={inputStyle} data-testid="verify-block-input" />
          <input placeholder="PAN" value={verifyPan} onChange={(e) => setVerifyPan(e.target.value)} style={inputStyle} data-testid="verify-pan-input" />
          <input placeholder="Expected PIN" value={expectedPin} onChange={(e) => setExpectedPin(e.target.value)} style={inputStyle} data-testid="verify-expected-input" />
          <button onClick={handleVerify} disabled={!verifyBlock || !verifyPan || !expectedPin || !!loading} style={btnStyle(!!verifyBlock && !!verifyPan && !!expectedPin && !loading)} data-testid="verify-btn">
            {loading === 'verify' ? <RefreshCw size={14} className="spin" /> : 'Verify PIN'}
          </button>
          {verificationResult && (
            <div style={{ marginTop: 4, padding: 8, borderRadius: 4, fontSize: 13, background: verificationResult.verified ? '#22C55E18' : '#EF444418', border: `1px solid ${verificationResult.verified ? '#22C55E40' : '#EF444440'}`, color: verificationResult.verified ? '#22C55E' : '#EF4444' }}>
              {verificationResult.verified ? 'PIN Verified' : 'PIN Mismatch'}: {verificationResult.message}
            </div>
          )}
        </div>
      </div>

      {error && (
        <div style={{ marginTop: 12, color: '#EF4444', fontSize: 12, padding: '8px 12px', background: '#EF444418', borderRadius: 4, border: '1px solid #EF444440' }}>
          {error}
        </div>
      )}
    </div>
  );
}
