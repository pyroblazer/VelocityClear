import { useState } from 'react';
import { Code, Play, CheckCircle, AlertCircle, Key } from 'lucide-react';
import { fetchWithAuth } from '../lib/apiClient';
import { logger } from '../lib/logger';

const CODE_BLOCK =
  'bg-[#0A0A0A] border border-[#2A2A2A] rounded-lg p-4 font-mono text-xs leading-relaxed text-[#A1A1AA] whitespace-pre-wrap break-all';

export default function IntegrationDemoPage() {
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
      const msg = err instanceof Error ? err.message : String(err);
      setLastResponse(`Error: ${msg}`);
      logger.error(`API demo step failed: ${label}`, msg);
    }
    setLoading('');
  };

  const step1 = () =>
    runStep('Authenticating...', async () => {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: 'admin', password: 'admin123' }),
      });
      if (!res.ok) throw new Error(`${res.status}`);
      const data: Record<string, unknown> = await res.json();
      if (!data || typeof data !== 'object') throw new Error('Invalid response from server');
      setJwt(String(data.token ?? ''));
      return JSON.stringify(data, null, 2);
    });

  const step2 = () =>
    runStep('Creating transaction...', async () => {
      const res = await fetchWithAuth('/api/transactions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userId: 'admin',
          amount: 150.0,
          currency: 'USD',
          description: 'Integration demo payment',
        }),
      });
      if (!res.ok) throw new Error(`${res.status}`);
      const data: Record<string, unknown> = await res.json();
      if (!data || typeof data !== 'object') throw new Error('Invalid response from server');
      setLastCreatedId(String(data.id ?? ''));
      return JSON.stringify(data, null, 2);
    });

  const step3 = () =>
    runStep('Checking status...', async () => {
      const id = lastCreatedId || 'unknown';
      const res = await fetchWithAuth(`/api/transactions/${id}`);
      if (!res.ok) throw new Error(`${res.status}`);
      const data: Record<string, unknown> = await res.json();
      return JSON.stringify(data, null, 2);
    });

  const step4 = () =>
    runStep('Using API key...', async () => {
      const res = await fetch('/api/transactions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-API-Key': apiKey,
        },
        body: JSON.stringify({
          userId: 'admin',
          amount: 75.0,
          currency: 'USD',
          description: 'API key payment',
        }),
      });
      if (!res.ok) throw new Error(`${res.status}`);
      const data: Record<string, unknown> = await res.json();
      if (!data || typeof data !== 'object') throw new Error('Invalid response from server');
      setLastCreatedId(String(data.id ?? ''));
      return JSON.stringify(data, null, 2);
    });

  return (
    <div className="max-w-[900px] mx-auto p-8">
      <div className="flex items-center gap-3 mb-2">
        <Code size={22} className="text-[#F59E0B]" />
        <h1 className="text-xl font-bold">API Integration Guide</h1>
      </div>
      <p className="text-[#A1A1AA] text-sm mb-8">
        Step-by-step guide for integrating with the VelocityClear payment API.
      </p>

      {/* Step 1 */}
      <section className="mb-6 bg-[#141414] border border-[#2A2A2A] rounded-xl p-6">
        <h2 className="text-base font-semibold mb-2">
          Step 1: Authenticate
        </h2>
        <p className="text-xs text-[#A1A1AA] mb-4">
          Obtain a JWT token by sending credentials to the login endpoint.
        </p>
        <pre className={CODE_BLOCK}>{`const response = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    username: 'admin',
    password: 'admin123'
  })
});
const { token, role, expiresAt } = await response.json();`}</pre>
        <div className="mt-3 flex items-center gap-3">
          <button
            onClick={step1}
            disabled={!!loading}
            className="px-5 py-2 bg-[#3B82F6] text-white rounded-md text-sm font-semibold disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            <Play size={13} />
            {loading === 'Authenticating...' ? 'Running...' : 'Run This'}
          </button>
          {jwt && (
            <span className="text-xs text-[#22C55E] flex items-center gap-1">
              <CheckCircle size={12} /> Token received
            </span>
          )}
        </div>
      </section>

      {/* Step 2 */}
      <section className="mb-6 bg-[#141414] border border-[#2A2A2A] rounded-xl p-6">
        <h2 className="text-base font-semibold mb-2">
          Step 2: Create a Transaction
        </h2>
        <p className="text-xs text-[#A1A1AA] mb-4">
          Use the JWT token to create a payment transaction.
        </p>
        <pre className={CODE_BLOCK}>{`const response = await fetch('/api/transactions', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': 'Bearer <your-jwt-token>'
  },
  body: JSON.stringify({
    userId: 'admin', amount: 150.00,
    currency: 'USD', description: 'Payment for order #123'
  })
});
const transaction = await response.json();`}</pre>
        <div className="mt-3">
          <button
            onClick={step2}
            disabled={!!loading || !jwt}
            className="px-5 py-2 bg-[#3B82F6] text-white rounded-md text-sm font-semibold disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            <Play size={13} />
            {loading === 'Creating transaction...' ? 'Running...' : 'Run This'}
          </button>
          {!jwt && (
            <span className="text-xs text-[#F59E0B] ml-3">
              Complete Step 1 first
            </span>
          )}
        </div>
      </section>

      {/* Step 3 */}
      <section className="mb-6 bg-[#141414] border border-[#2A2A2A] rounded-xl p-6">
        <h2 className="text-base font-semibold mb-2">
          Step 3: Check Transaction Status
        </h2>
        <p className="text-xs text-[#A1A1AA] mb-4">
          Poll the transaction endpoint to check the current status.
        </p>
        <pre className={CODE_BLOCK}>{`const response = await fetch('/api/transactions/<transaction-id>', {
  headers: { 'Authorization': 'Bearer <your-jwt-token>' }
});
// status can be: Pending, Processing, Approved, Rejected, Completed`}</pre>
        <div className="mt-3">
          <button
            onClick={step3}
            disabled={!!loading || !jwt}
            className="px-5 py-2 bg-[#3B82F6] text-white rounded-md text-sm font-semibold disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            <Play size={13} />
            {loading === 'Checking status...' ? 'Running...' : 'Run This'}
          </button>
        </div>
      </section>

      {/* Step 4 */}
      <section className="mb-6 bg-[#141414] border border-[#2A2A2A] rounded-xl p-6">
        <h2 className="text-base font-semibold mb-2 flex items-center gap-2">
          <Key size={15} /> Step 4: Using an API Key
        </h2>
        <p className="text-xs text-[#A1A1AA] mb-4">
          For server-to-server integration, use an API key instead of JWT.
          Generate keys from the Settings page.
        </p>
        <div className="mb-3">
          <label className="text-xs text-[#A1A1AA] block mb-1">
            Your API Key
          </label>
          <input
            type="text"
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            placeholder="vc_live_..."
            className="w-full px-3 py-2 bg-[#0A0A0A] border border-[#2A2A2A] rounded-md text-white text-sm outline-none"
          />
        </div>
        <pre className={CODE_BLOCK}>{`const response = await fetch('/api/transactions', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'X-API-Key': 'vc_live_your_api_key_here'
  },
  body: JSON.stringify({
    userId: 'admin', amount: 75.00,
    currency: 'USD', description: 'API key payment'
  })
});`}</pre>
        <div className="mt-3">
          <button
            onClick={step4}
            disabled={!!loading || !apiKey}
            className="px-5 py-2 bg-[#3B82F6] text-white rounded-md text-sm font-semibold disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            <Play size={13} />
            {loading === 'Using API key...' ? 'Running...' : 'Run This'}
          </button>
        </div>
      </section>

      {/* Response panel */}
      {lastResponse && (
        <section className="mb-6">
          <div className="flex items-center gap-2 mb-2">
            {lastResponse.startsWith('Error') ? (
              <AlertCircle size={15} className="text-[#EF4444]" />
            ) : (
              <CheckCircle size={15} className="text-[#22C55E]" />
            )}
            <h2 className="text-base font-semibold">Response</h2>
          </div>
          <pre className="bg-[#0A0A0A] border border-[#22C55E40] rounded-lg p-4 font-mono text-xs leading-relaxed text-[#22C55E] whitespace-pre-wrap break-all">
            {lastResponse}
          </pre>
        </section>
      )}
    </div>
  );
}
