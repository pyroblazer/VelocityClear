import { useState, type FormEvent } from 'react';
import { createTransaction } from '../lib/transactionApi';
import { listHsmKeys, generateKey, encryptPin } from '../lib/cardApi';
import { logger } from '../lib/logger';
import { useTransactionStore } from '../stores/transactionStore';
import { Send, CheckCircle, XCircle, Loader2, CreditCard, ArrowRightLeft } from 'lucide-react';

const CURRENCIES = ['USD', 'EUR', 'GBP', 'JPY', 'CAD', 'AUD', 'CHF', 'CNY'];
const CARD_TYPES = ['Credit', 'Debit', 'Prepaid'];

type PaymentMode = 'transfer' | 'card';

export default function TransactionForm() {
  const addTransaction = useTransactionStore((s) => s.addTransaction);
  const [mode, setMode] = useState<PaymentMode>('transfer');
  const [form, setForm] = useState({
    userId: '', amount: '', currency: 'USD', description: '', counterparty: '',
    pan: '', pin: '', cardType: 'Credit',
  });
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [statusText, setStatusText] = useState('');
  const [errorMsg, setErrorMsg] = useState('');

  const formatPan = (val: string) => val.replace(/\D/g, '').slice(0, 16).replace(/(\d{4})/g, '$1 ').trim();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setStatus('loading');
    setErrorMsg('');

    try {
      if (mode === 'card') {
        // Card payment: encrypt PIN transparently, then submit
        setStatusText('Preparing payment...');

        const pan = form.pan.replace(/\s/g, '');
        if (pan.length < 13) throw new Error('Card number must be at least 13 digits');

        // Get or create a ZPK
        setStatusText('Accessing secure key store...');
        const keys = await listHsmKeys();
        const keyList = Array.isArray(keys) ? keys : [];
        let zpkId = keyList.find((k: string) => k.includes('zpk') || k.includes('ZPK'));
        if (!zpkId) {
          const genResult = await generateKey('ZPK', 'default-zpk');
          zpkId = genResult.keyId;
        }

        // Encrypt PIN
        setStatusText('Encrypting credentials...');
        const encResult = await encryptPin(form.pin, pan, zpkId);

        // Submit transaction with card fields
        setStatusText('Authorizing payment...');
        const result = await createTransaction({
          userId: form.userId,
          amount: parseFloat(form.amount),
          currency: form.currency,
          description: form.description || undefined,
          counterparty: form.counterparty || undefined,
          pan,
          pinBlock: encResult.encryptedPinBlock,
          cardType: form.cardType,
        });

        if (result.error) throw new Error(result.error);
        addTransaction(result);
      } else {
        // Transfer
        setStatusText('Submitting transfer...');
        const result = await createTransaction({
          userId: form.userId,
          amount: parseFloat(form.amount),
          currency: form.currency,
          description: form.description || undefined,
          counterparty: form.counterparty || undefined,
        });

        if (result.error) throw new Error(result.error);
        addTransaction(result);
      }

      setStatus('success');
      setForm({ userId: '', amount: '', currency: 'USD', description: '', counterparty: '', pan: '', pin: '', cardType: 'Credit' });
      setTimeout(() => { setStatus('idle'); setStatusText(''); }, 3000);
    } catch (err) {
      setStatus('error');
      const msg = err instanceof Error ? err.message : 'Transaction failed';
      setErrorMsg(msg);
      logger.error('Transaction failed', msg);
    }
  };

  const inputStyle: React.CSSProperties = {
    width: '100%', padding: '10px 14px', background: '#0A0A0A',
    border: '1px solid #2A2A2A', borderRadius: 8, color: '#FFFFFF',
    fontSize: 14, outline: 'none', transition: 'border-color 0.2s', boxSizing: 'border-box',
  };

  const labelStyle: React.CSSProperties = {
    display: 'block', fontSize: 12, fontWeight: 600, color: '#A1A1AA',
    marginBottom: 6, textTransform: 'uppercase', letterSpacing: 0.05,
  };

  const isValid = form.userId.trim() && form.amount && parseFloat(form.amount) > 0
    && (mode === 'transfer' || (form.pan.replace(/\s/g, '').length >= 13 && form.pin.length >= 4));

  return (
    <div style={{ background: '#1A1A1A', borderRadius: 12, border: '1px solid #2A2A2A', padding: 24 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 20 }}>
        <Send size={20} style={{ color: '#3B82F6' }} />
        <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: '#FFFFFF' }}>New Transaction</h2>
      </div>

      {/* Mode toggle */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 20 }}>
        <button
          type="button"
          onClick={() => setMode('transfer')}
          style={{
            flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
            padding: '10px 16px', borderRadius: 8, fontSize: 13, fontWeight: 600, cursor: 'pointer',
            background: mode === 'transfer' ? '#3B82F6' : '#0A0A0A',
            color: mode === 'transfer' ? '#FFF' : '#A1A1AA',
            border: `1px solid ${mode === 'transfer' ? '#3B82F6' : '#2A2A2A'}`,
          }}
        >
          <ArrowRightLeft size={14} /> Transfer
        </button>
        <button
          type="button"
          onClick={() => setMode('card')}
          style={{
            flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
            padding: '10px 16px', borderRadius: 8, fontSize: 13, fontWeight: 600, cursor: 'pointer',
            background: mode === 'card' ? '#8B5CF6' : '#0A0A0A',
            color: mode === 'card' ? '#FFF' : '#A1A1AA',
            border: `1px solid ${mode === 'card' ? '#8B5CF6' : '#2A2A2A'}`,
          }}
        >
          <CreditCard size={14} /> Pay with Card
        </button>
      </div>

      {status === 'success' && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '12px 16px', background: '#22C55E18', border: '1px solid #22C55E40', borderRadius: 8, marginBottom: 16, fontSize: 13, color: '#22C55E' }}>
          <CheckCircle size={16} />
          {mode === 'card' ? 'Payment authorized successfully' : 'Transfer submitted successfully'}
        </div>
      )}
      {status === 'error' && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '12px 16px', background: '#EF444418', border: '1px solid #EF444440', borderRadius: 8, marginBottom: 16, fontSize: 13, color: '#EF4444' }}>
          <XCircle size={16} />
          {errorMsg}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <div>
            <label style={labelStyle}>User ID *</label>
            <input style={inputStyle} type="text" placeholder="Enter user ID" value={form.userId}
              onChange={(e) => setForm({ ...form, userId: e.target.value })}
              onFocus={(e) => (e.target.style.borderColor = '#3B82F6')} onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
              required data-testid="txn-user-id" />
          </div>
          <div style={{ display: 'flex', gap: 12 }}>
            <div style={{ flex: 1 }}>
              <label style={labelStyle}>Amount *</label>
              <input style={inputStyle} type="number" step="0.01" min="0.01" placeholder="0.00" value={form.amount}
                onChange={(e) => setForm({ ...form, amount: e.target.value })}
                onFocus={(e) => (e.target.style.borderColor = '#3B82F6')} onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
                required data-testid="txn-amount" />
            </div>
            <div style={{ flex: 1 }}>
              <label style={labelStyle}>Currency</label>
              <select style={{ ...inputStyle, cursor: 'pointer' }} value={form.currency}
                onChange={(e) => setForm({ ...form, currency: e.target.value })} data-testid="txn-currency">
                {CURRENCIES.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </div>
          </div>

          {mode === 'card' && (
            <>
              <div>
                <label style={labelStyle}>Card Number *</label>
                <input style={inputStyle} type="text" placeholder="4111 1111 1111 1111" value={form.pan}
                  onChange={(e) => setForm({ ...form, pan: formatPan(e.target.value) })}
                  onFocus={(e) => (e.target.style.borderColor = '#8B5CF6')} onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
                  required data-testid="txn-pan" />
              </div>
              <div style={{ display: 'flex', gap: 12 }}>
                <div style={{ flex: 1 }}>
                  <label style={labelStyle}>PIN *</label>
                  <input style={inputStyle} type="password" placeholder="Enter PIN" value={form.pin}
                    onChange={(e) => setForm({ ...form, pin: e.target.value.replace(/\D/g, '').slice(0, 6) })}
                    onFocus={(e) => (e.target.style.borderColor = '#8B5CF6')} onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
                    required data-testid="txn-pin" />
                </div>
                <div style={{ flex: 1 }}>
                  <label style={labelStyle}>Card Type</label>
                  <select style={{ ...inputStyle, cursor: 'pointer' }} value={form.cardType}
                    onChange={(e) => setForm({ ...form, cardType: e.target.value })} data-testid="txn-card-type">
                    {CARD_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                  </select>
                </div>
              </div>
            </>
          )}

          {mode === 'transfer' && (
            <div>
              <label style={labelStyle}>Transfer To</label>
              <input style={inputStyle} type="text" placeholder="Recipient account or identifier" value={form.counterparty}
                onChange={(e) => setForm({ ...form, counterparty: e.target.value })}
                onFocus={(e) => (e.target.style.borderColor = '#3B82F6')} onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
                data-testid="txn-counterparty" />
            </div>
          )}

          <div>
            <label style={labelStyle}>Description</label>
            <input style={inputStyle} type="text" placeholder="Optional description" value={form.description}
              onChange={(e) => setForm({ ...form, description: e.target.value })}
              onFocus={(e) => (e.target.style.borderColor = mode === 'card' ? '#8B5CF6' : '#3B82F6')}
              onBlur={(e) => (e.target.style.borderColor = '#2A2A2A')}
              data-testid="txn-description" />
          </div>

          <button type="submit" disabled={!isValid || status === 'loading'} data-testid="txn-submit"
            style={{
              display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
              width: '100%', padding: '12px 20px',
              background: isValid && status !== 'loading' ? (mode === 'card' ? '#8B5CF6' : '#3B82F6') : '#2A2A2A',
              color: isValid && status !== 'loading' ? '#FFFFFF' : '#666666',
              border: 'none', borderRadius: 8, fontSize: 14, fontWeight: 600,
              cursor: isValid && status !== 'loading' ? 'pointer' : 'not-allowed',
            }}
          >
            {status === 'loading' ? (
              <><Loader2 size={16} style={{ animation: 'spin 1s linear infinite' }} /> {statusText}</>
            ) : mode === 'card' ? (
              <><CreditCard size={16} /> Pay Now</>
            ) : (
              <><Send size={16} /> Submit Transfer</>
            )}
          </button>
        </div>
      </form>
    </div>
  );
}
