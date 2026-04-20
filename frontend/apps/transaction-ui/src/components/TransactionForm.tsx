import { useState, type FormEvent } from 'react';
import { createTransaction } from '../lib/api';
import { useTransactionStore } from '../stores/transactionStore';
import { Send, CheckCircle, XCircle, Loader2 } from 'lucide-react';

const CURRENCIES = ['USD', 'EUR', 'GBP', 'JPY', 'CAD', 'AUD', 'CHF', 'CNY'];

export default function TransactionForm() {
  const addTransaction = useTransactionStore((s) => s.addTransaction);
  const [form, setForm] = useState({
    userId: '',
    amount: '',
    currency: 'USD',
    description: '',
    counterparty: '',
  });
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [errorMsg, setErrorMsg] = useState('');

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setStatus('loading');
    setErrorMsg('');

    try {
      const result = await createTransaction({
        userId: form.userId,
        amount: parseFloat(form.amount),
        currency: form.currency,
        description: form.description || undefined,
        counterparty: form.counterparty || undefined,
      });

      if (result.error) {
        setStatus('error');
        setErrorMsg(result.error || 'Transaction failed');
        return;
      }

      addTransaction(result);
      setStatus('success');
      setForm({ userId: '', amount: '', currency: 'USD', description: '', counterparty: '' });

      setTimeout(() => setStatus('idle'), 3000);
    } catch {
      setStatus('error');
      setErrorMsg('Network error. Please try again.');
    }
  };

  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '10px 14px',
    background: '#0A0A0A',
    border: '1px solid #2A2A2A',
    borderRadius: 8,
    color: '#FFFFFF',
    fontSize: 14,
    outline: 'none',
    transition: 'border-color 0.2s',
  };

  const labelStyle: React.CSSProperties = {
    display: 'block',
    fontSize: 12,
    fontWeight: 600,
    color: '#A1A1AA',
    marginBottom: 6,
    textTransform: 'uppercase',
    letterSpacing: 0.05,
  };

  const isValid = form.userId.trim() && form.amount && parseFloat(form.amount) > 0;

  return (
    <div
      style={{
        background: '#1A1A1A',
        borderRadius: 12,
        border: '1px solid #2A2A2A',
        padding: 24,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 20 }}>
        <Send size={20} style={{ color: '#3B82F6' }} />
        <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: '#FFFFFF' }}>
          New Transaction
        </h2>
      </div>

      {status === 'success' && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            padding: '12px 16px',
            background: '#22C55E18',
            border: '1px solid #22C55E40',
            borderRadius: 8,
            marginBottom: 16,
            fontSize: 13,
            color: '#22C55E',
          }}
        >
          <CheckCircle size={16} />
          Transaction submitted successfully
        </div>
      )}

      {status === 'error' && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            padding: '12px 16px',
            background: '#EF444418',
            border: '1px solid #EF444440',
            borderRadius: 8,
            marginBottom: 16,
            fontSize: 13,
            color: '#EF4444',
          }}
        >
          <XCircle size={16} />
          {errorMsg}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div>
            <label style={labelStyle}>User ID *</label>
            <input
              style={inputStyle}
              type="text"
              placeholder="Enter user ID"
              value={form.userId}
              onChange={(e) => setForm({ ...form, userId: e.target.value })}
              onFocus={(e) => (e.target.style.borderColor = '#3B82F6')}
              onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
              required
            />
          </div>

          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}>
              <label style={labelStyle}>Amount *</label>
              <input
                style={inputStyle}
                type="number"
                step="0.01"
                min="0.01"
                placeholder="0.00"
                value={form.amount}
                onChange={(e) => setForm({ ...form, amount: e.target.value })}
                onFocus={(e) => (e.target.style.borderColor = '#3B82F6')}
                onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
                required
              />
            </div>
            <div style={{ flex: 1 }}>
              <label style={labelStyle}>Currency</label>
              <select
                style={{ ...inputStyle, cursor: 'pointer' }}
                value={form.currency}
                onChange={(e) => setForm({ ...form, currency: e.target.value })}
              >
                {CURRENCIES.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label style={labelStyle}>Description</label>
            <input
              style={inputStyle}
              type="text"
              placeholder="Optional description"
              value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
              onFocus={(e) => (e.target.style.borderColor = '#3B82F6')}
              onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
            />
          </div>

          <div>
            <label style={labelStyle}>Counterparty</label>
            <input
              style={inputStyle}
              type="text"
              placeholder="Optional counterparty"
              value={form.counterparty}
              onChange={(e) => setForm({ ...form, counterparty: e.target.value })}
              onFocus={(e) => (e.target.style.borderColor = '#3B82F6')}
              onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
            />
          </div>

          <button
            type="submit"
            disabled={!isValid || status === 'loading'}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
              width: '100%',
              padding: '12px 20px',
              background: isValid && status !== 'loading' ? '#3B82F6' : '#2A2A2A',
              color: isValid && status !== 'loading' ? '#FFFFFF' : '#666666',
              border: 'none',
              borderRadius: 8,
              fontSize: 14,
              fontWeight: 600,
              cursor: isValid && status !== 'loading' ? 'pointer' : 'not-allowed',
              transition: 'background 0.2s',
            }}
          >
            {status === 'loading' ? (
              <>
                <Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} />
                Processing...
              </>
            ) : (
              <>
                <Send size={16} />
                Submit Transaction
              </>
            )}
          </button>
        </div>
      </form>

      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}
