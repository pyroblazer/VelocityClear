import { useState } from 'react';

const CODE_STYLE: React.CSSProperties = {
  background: '#0A0A0A',
  border: '1px solid #2A2A2A',
  borderRadius: 8,
  padding: 16,
  fontFamily: 'monospace',
  fontSize: 12,
  lineHeight: 1.6,
  color: '#A1A1AA',
  whiteSpace: 'pre-wrap',
  wordBreak: 'break-all',
};

const RESPONSE_STYLE: React.CSSProperties = {
  ...CODE_STYLE,
  borderColor: '#22C55E40',
  color: '#22C55E',
};

export default function App() {
  const [jwt, setJwt] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [lastResponse, setLastResponse] = useState('');
  const [lastCreatedId, setLastCreatedId] = useState('');
  const [loading, setLoading] = useState('');

  const runStep = async (label: string, fn: () => Promise<string>) => {
    setLoading(label);
    try {
      const result = await fn();
      setLastResponse(result);
    } catch (err) {
      setLastResponse(`Error: ${err instanceof Error ? err.message : String(err)}`);
    }
    setLoading('');
  };

  const step1 = () => runStep('Authenticating...', async () => {
    const res = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: 'admin', password: 'admin123' }),
    });
    if (!res.ok) throw new Error(`${res.status}`);
    const data = await res.json();
    setJwt(data.token);
    return JSON.stringify(data, null, 2);
  });

  const step2 = () => runStep('Creating transaction...', async () => {
    const res = await fetch('/api/transactions', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${jwt}`,
      },
      body: JSON.stringify({
        userId: 'admin',
        amount: 150.00,
        currency: 'USD',
        description: 'Integration demo payment',
      }),
    });
    if (!res.ok) throw new Error(`${res.status}`);
    const data = await res.json();
    setLastCreatedId(data.id);
    return JSON.stringify(data, null, 2);
  });

  const step3 = () => runStep('Checking status...', async () => {
    const id = lastCreatedId || 'unknown';
    const res = await fetch(`/api/transactions/${id}`, {
      headers: { 'Authorization': `Bearer ${jwt}` },
    });
    if (!res.ok) throw new Error(`${res.status}`);
    const data = await res.json();
    return JSON.stringify(data, null, 2);
  });

  const step4 = () => runStep('Using API key...', async () => {
    const res = await fetch('/api/transactions', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-API-Key': apiKey,
      },
      body: JSON.stringify({
        userId: 'admin',
        amount: 75.00,
        currency: 'USD',
        description: 'API key payment',
      }),
    });
    if (!res.ok) throw new Error(`${res.status}`);
    const data = await res.json();
    setLastCreatedId(data.id);
    return JSON.stringify(data, null, 2);
  });

  return (
    <div style={{ maxWidth: 900, margin: '0 auto', padding: 40, fontFamily: 'system-ui, sans-serif', background: '#0A0A0A', color: '#FFFFFF', minHeight: '100vh' }}>
      <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 8 }}>
        VelocityClear API Integration Demo
      </h1>
      <p style={{ color: '#A1A1AA', fontSize: 14, marginBottom: 32 }}>
        A step-by-step guide for integrating with the VelocityClear payment API. Designed for junior developers.
      </p>

      {/* Step 1 */}
      <section style={{ marginBottom: 32, background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 24 }}>
        <h2 style={{ fontSize: 16, fontWeight: 600, marginBottom: 8 }}>Step 1: Authenticate</h2>
        <p style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 16 }}>
          First, obtain a JWT token by sending your credentials to the login endpoint.
        </p>
        <pre style={CODE_STYLE}>{`const response = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    username: 'admin',
    password: 'admin123'
  })
});
const { token, role, expiresAt } = await response.json();
// Store the token for subsequent requests`}</pre>
        <div style={{ marginTop: 12, display: 'flex', alignItems: 'center', gap: 12 }}>
          <button onClick={step1} disabled={!!loading} style={{ padding: '8px 20px', background: '#3B82F6', color: '#FFF', border: 'none', borderRadius: 6, cursor: loading ? 'not-allowed' : 'pointer', fontSize: 13, fontWeight: 600, opacity: loading ? 0.5 : 1 }}>
            {loading === 'Authenticating...' ? 'Running...' : 'Run This'}
          </button>
          {jwt && <span style={{ fontSize: 12, color: '#22C55E' }}>Token received</span>}
        </div>
      </section>

      {/* Step 2 */}
      <section style={{ marginBottom: 32, background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 24 }}>
        <h2 style={{ fontSize: 16, fontWeight: 600, marginBottom: 8 }}>Step 2: Create a Transaction</h2>
        <p style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 16 }}>
          Use the JWT token to create a payment transaction. Include the Authorization header with "Bearer" prefix.
        </p>
        <pre style={CODE_STYLE}>{`const response = await fetch('/api/transactions', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': 'Bearer <your-jwt-token>'
  },
  body: JSON.stringify({
    userId: 'admin',
    amount: 150.00,
    currency: 'USD',
    description: 'Payment for order #123'
  })
});
const transaction = await response.json();`}</pre>
        <div style={{ marginTop: 12 }}>
          <button onClick={step2} disabled={!!loading || !jwt} style={{ padding: '8px 20px', background: !jwt ? '#2A2A2A' : '#3B82F6', color: !jwt ? '#666' : '#FFF', border: 'none', borderRadius: 6, cursor: loading || !jwt ? 'not-allowed' : 'pointer', fontSize: 13, fontWeight: 600 }}>
            {loading === 'Creating transaction...' ? 'Running...' : 'Run This'}
          </button>
          {!jwt && <span style={{ fontSize: 12, color: '#F59E0B', marginLeft: 12 }}>Complete Step 1 first</span>}
        </div>
      </section>

      {/* Step 3 */}
      <section style={{ marginBottom: 32, background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 24 }}>
        <h2 style={{ fontSize: 16, fontWeight: 600, marginBottom: 8 }}>Step 3: Check Transaction Status</h2>
        <p style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 16 }}>
          Poll the transaction endpoint to check the current status. The backend event pipeline processes the transaction through risk evaluation and payment authorization.
        </p>
        <pre style={CODE_STYLE}>{`const response = await fetch('/api/transactions/<transaction-id>', {
  headers: { 'Authorization': 'Bearer <your-jwt-token>' }
});
const transaction = await response.json();
// transaction.status can be: Pending, Processing, Approved, Rejected, Completed`}</pre>
        <div style={{ marginTop: 12 }}>
          <button onClick={step3} disabled={!!loading || !jwt} style={{ padding: '8px 20px', background: !jwt ? '#2A2A2A' : '#3B82F6', color: !jwt ? '#666' : '#FFF', border: 'none', borderRadius: 6, cursor: loading || !jwt ? 'not-allowed' : 'pointer', fontSize: 13, fontWeight: 600 }}>
            {loading === 'Checking status...' ? 'Running...' : 'Run This'}
          </button>
        </div>
      </section>

      {/* Step 4 */}
      <section style={{ marginBottom: 32, background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 24 }}>
        <h2 style={{ fontSize: 16, fontWeight: 600, marginBottom: 8 }}>Step 4: Using an API Key</h2>
        <p style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 16 }}>
          For server-to-server integration, use an API key instead of JWT. Generate keys from the Settings page in the main app. Send the key in the <code style={{ color: '#3B82F6' }}>X-API-Key</code> header.
        </p>
        <div style={{ marginBottom: 12 }}>
          <label style={{ fontSize: 12, color: '#A1A1AA', display: 'block', marginBottom: 4 }}>Your API Key</label>
          <input
            type="text"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            placeholder="vc_live_..."
            style={{ width: '100%', padding: '8px 12px', background: '#0A0A0A', border: '1px solid #2A2A2A', borderRadius: 6, color: '#FFF', fontSize: 13, outline: 'none', boxSizing: 'border-box' }}
          />
        </div>
        <pre style={CODE_STYLE}>{`const response = await fetch('/api/transactions', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-API-Key': 'vc_live_your_api_key_here'
  },
  body: JSON.stringify({
    userId: 'admin',
    amount: 75.00,
    currency: 'USD',
    description: 'API key payment'
  })
});`}</pre>
        <div style={{ marginTop: 12 }}>
          <button onClick={step4} disabled={!!loading || !apiKey} style={{ padding: '8px 20px', background: !apiKey ? '#2A2A2A' : '#3B82F6', color: !apiKey ? '#666' : '#FFF', border: 'none', borderRadius: 6, cursor: loading || !apiKey ? 'not-allowed' : 'pointer', fontSize: 13, fontWeight: 600 }}>
            {loading === 'Using API key...' ? 'Running...' : 'Run This'}
          </button>
        </div>
      </section>

      {/* Response panel */}
      {lastResponse && (
        <section style={{ marginBottom: 32 }}>
          <h2 style={{ fontSize: 16, fontWeight: 600, marginBottom: 8 }}>Response</h2>
          <pre style={RESPONSE_STYLE}>{lastResponse}</pre>
        </section>
      )}

      <footer style={{ borderTop: '1px solid #2A2A2A', paddingTop: 16, color: '#A1A1AA', fontSize: 12 }}>
        <p>VelocityClear API Integration Demo — For developer documentation, see the main app at <a href="http://localhost:3000" style={{ color: '#3B82F6' }}>localhost:3000</a>.</p>
        <p style={{ marginTop: 8 }}>
          <strong>Quick Reference:</strong> API Base URL is <code style={{ color: '#3B82F6' }}>http://localhost:5000</code> (gateway) or use relative paths with this demo's proxy.
        </p>
      </footer>
    </div>
  );
}
