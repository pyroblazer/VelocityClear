import { useState } from 'react';
import { useCardStore } from '../stores/cardStore';
import { parseIso8583, buildIso8583, authorizeCard } from '../lib/cardApi';
import { FileText, RefreshCw } from 'lucide-react';

const cardStyle: React.CSSProperties = { background: '#1A1A1A', borderRadius: 8, border: '1px solid #2A2A2A', padding: 20 };
const inputStyle: React.CSSProperties = { background: '#0A0A0A', border: '1px solid #2A2A2A', borderRadius: 6, padding: '8px 12px', color: '#FFFFFF', fontSize: 13, width: '100%', boxSizing: 'border-box', fontFamily: 'monospace' };
const btnStyle = (enabled: boolean): React.CSSProperties => ({ background: enabled ? '#3B82F6' : '#2A2A2A', color: enabled ? '#FFFFFF' : '#666666', border: 'none', borderRadius: 6, padding: '8px 16px', cursor: enabled ? 'pointer' : 'not-allowed', fontSize: 13, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6 });

export default function Iso8583Tools() {
  const { parsedMessage, setParsedMessage, builtMessage, setBuiltMessage, authResult, setAuthResult, setError, error } = useCardStore();
  const [isoInput, setIsoInput] = useState('');
  const [loading, setLoading] = useState('');
  const [buildMti, setBuildMti] = useState('0100');
  const [buildFields, setBuildFields] = useState('{\n  "2": "4111111111111111",\n  "4": "000000001000",\n  "49": "USD"\n}');
  const [authPan, setAuthPan] = useState('4111111111111111');
  const [authAmount, setAuthAmount] = useState('100.00');
  const [authCurrency, setAuthCurrency] = useState('USD');
  const [authPinBlock, setAuthPinBlock] = useState('');

  const handleParse = async () => {
    if (!isoInput.trim()) return;
    setLoading('parse');
    setError(null);
    try {
      const result = await parseIso8583(isoInput);
      setParsedMessage(result);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Parse failed');
    } finally { setLoading(''); }
  };

  const handleBuild = async () => {
    setLoading('build');
    setError(null);
    try {
      const fields = JSON.parse(buildFields);
      const result = await buildIso8583(buildMti, fields);
      setBuiltMessage(result.isoMessage);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Build failed - check JSON format');
    } finally { setLoading(''); }
  };

  const handleAuthorize = async () => {
    if (!authPan) return;
    setLoading('authorize');
    setError(null);
    try {
      const result = await authorizeCard({ pan: authPan, amount: parseFloat(authAmount), currency: authCurrency, encryptedPinBlock: authPinBlock || '0000000000000000', zpkId: 'default-zpk', terminalId: 'TERM001', merchantId: 'MERCHANT001' });
      setAuthResult(result);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Authorization failed');
    } finally { setLoading(''); }
  };

  return (
    <div style={cardStyle}>
      <h2 style={{ margin: '0 0 16px 0', fontSize: 16, fontWeight: 600, display: 'flex', alignItems: 'center', gap: 8 }}>
        <FileText size={18} style={{ color: '#F59E0B' }} />
        ISO 8583 Tools
      </h2>
      <p style={{ margin: '0 0 12px 0', fontSize: 12, color: '#888888', lineHeight: 1.5 }}>
        ISO 8583 is the language ATMs and banks use to talk to each other, like a fill-in-the-blank form
        where each blank (field) has a specific meaning (card number, amount, currency, etc.).
      </p>
      <details style={{ marginBottom: 12, fontSize: 11, color: '#666666' }}>
        <summary style={{ cursor: 'pointer', color: '#A1A1AA', fontSize: 11 }}>How ISO 8583 messages are structured</summary>
        <div style={{ marginTop: 6, lineHeight: 1.5, padding: 8, background: '#0A0A0A', borderRadius: 4 }}>
          An ISO 8583 message has three parts:<br />
          <strong>MTI</strong> (Message Type Indicator, 4 digits): tells the receiver what kind of message this is. e.g. 0100 = auth request, 0110 = auth response, 0200 = financial request.<br />
          <strong>Primary Bitmap</strong> (16 hex chars): a bit field where each bit tells the receiver whether a specific field is present. Bit 1 = field 1 is present, bit 2 = field 2, etc.<br />
          <strong>Fields</strong>: variable-length data elements. Standard fields include: Field 2 = PAN (card number), Field 4 = amount (12 digits, zero-padded), Field 22 = point of entry mode, Field 49 = currency code.<br />
          <strong>Parse</strong> reads the MTI, decodes the bitmap to find which fields exist, then extracts each field's value.<br />
          <strong>Build</strong> does the reverse: takes an MTI and a field map, sets the bitmap bits, and concatenates everything into a single hex string.<br />
          <strong>Authorize</strong> builds a 0100 request, sends it to the HSM, and returns a simulated approval or decline with response code and auth ID.
        </div>
      </details>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
        {/* Parse */}
        <div>
          <div style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 8 }}>Parse Message</div>
          <textarea placeholder="Paste ISO 8583 message..." value={isoInput} onChange={(e) => setIsoInput(e.target.value)} style={{ ...inputStyle, minHeight: 80, resize: 'vertical' }} data-testid="parse-input" />
          <button onClick={handleParse} disabled={!isoInput.trim() || !!loading} style={{ ...btnStyle(!!isoInput.trim() && !loading), marginTop: 8, width: '100%' }} data-testid="parse-btn">
            {loading === 'parse' ? <RefreshCw size={14} style={{ animation: 'spin 1s linear infinite' }} /> : 'Parse'}
          </button>
          {parsedMessage && (
            <div style={{ marginTop: 8, background: '#0A0A0A', padding: 10, borderRadius: 4, fontSize: 12 }}>
              <div style={{ color: '#3B82F6', marginBottom: 4 }}>MTI: {parsedMessage.mti} ({parsedMessage.mtiDescription})</div>
              {Object.entries(parsedMessage.fields).map(([k, v]) => (
                <div key={k} style={{ color: '#A1A1AA', fontFamily: 'monospace' }}>Field {k}: <span style={{ color: '#FFFFFF' }}>{String(v)}</span></div>
              ))}
            </div>
          )}
        </div>

        {/* Build */}
        <div>
          <div style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 8 }}>Build Message</div>
          <input placeholder="MTI (e.g. 0100)" value={buildMti} onChange={(e) => setBuildMti(e.target.value)} style={{ ...inputStyle, marginBottom: 6 }} data-testid="build-mti-input" />
          <textarea placeholder='{"2": "4111...","4": "000000001000"}' value={buildFields} onChange={(e) => setBuildFields(e.target.value)} style={{ ...inputStyle, minHeight: 80, resize: 'vertical' }} data-testid="build-fields-input" />
          <button onClick={handleBuild} disabled={!!loading} style={{ ...btnStyle(!loading), marginTop: 8, width: '100%' }} data-testid="build-btn">
            {loading === 'build' ? <RefreshCw size={14} style={{ animation: 'spin 1s linear infinite' }} /> : 'Build'}
          </button>
          {builtMessage && <div style={{ marginTop: 8, background: '#0A0A0A', padding: 10, borderRadius: 4, fontFamily: 'monospace', fontSize: 12, color: '#22C55E', wordBreak: 'break-all' }}>{builtMessage}</div>}
        </div>

        {/* Authorize */}
        <div>
          <div style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 8 }}>Card Authorization</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <input placeholder="PAN" value={authPan} onChange={(e) => setAuthPan(e.target.value)} style={inputStyle} data-testid="auth-pan-input" />
            <input placeholder="Amount" value={authAmount} onChange={(e) => setAuthAmount(e.target.value)} style={inputStyle} data-testid="auth-amount-input" />
            <input placeholder="Currency (USD)" value={authCurrency} onChange={(e) => setAuthCurrency(e.target.value)} style={inputStyle} />
            <input placeholder="Encrypted PIN Block (optional)" value={authPinBlock} onChange={(e) => setAuthPinBlock(e.target.value)} style={inputStyle} />
            <button onClick={handleAuthorize} disabled={!authPan || !!loading} style={btnStyle(!!authPan && !loading)} data-testid="authorize-btn">
              {loading === 'authorize' ? <RefreshCw size={14} style={{ animation: 'spin 1s linear infinite' }} /> : 'Authorize Card'}
            </button>
            {authResult && (
              <div style={{ marginTop: 4, padding: 10, borderRadius: 4, fontSize: 12, background: authResult.approved ? '#22C55E18' : '#EF444418', border: `1px solid ${authResult.approved ? '#22C55E40' : '#EF444440'}`, color: authResult.approved ? '#22C55E' : '#EF4444' }}>
                <div style={{ fontWeight: 600 }}>{authResult.approved ? 'APPROVED' : 'DECLINED'}</div>
                <div>Response Code: {authResult.responseCode}</div>
                {authResult.authorizationId && <div>Auth ID: {authResult.authorizationId}</div>}
                <div>{authResult.message}</div>
              </div>
            )}
          </div>
        </div>
      </div>
      {error && <div style={{ marginTop: 12, color: '#EF4444', fontSize: 12, padding: '8px 12px', background: '#EF444418', borderRadius: 4, border: '1px solid #EF444440' }}>{error}</div>}
    </div>
  );
}
