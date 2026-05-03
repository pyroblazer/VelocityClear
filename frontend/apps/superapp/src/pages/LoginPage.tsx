import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Zap, LogIn, UserPlus } from 'lucide-react';
import { useAuthStore } from '../stores/authStore';
import { login, register } from '../lib/authApi';

export default function LoginPage() {
  const [isRegister, setIsRegister] = useState(false);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('User');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const authLogin = useAuthStore((s) => s.login);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const res = isRegister ? await register(username, password, role) : await login(username, password);
      authLogin(res.token, res.role, new Date(res.expiresAt));
      navigate('/transactions', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Authentication failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#0A0A0A' }}>
      <div style={{ width: 400, background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 32 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 24 }}>
          <Zap size={24} style={{ color: '#3B82F6' }} />
          <span style={{ fontWeight: 700, fontSize: 20, color: '#FFFFFF' }}>VelocityClear</span>
        </div>

        <h1 style={{ fontSize: 18, fontWeight: 600, color: '#FFFFFF', marginBottom: 4, display: 'flex', alignItems: 'center', gap: 8 }}>
          {isRegister ? <><UserPlus size={18} />Create Account</> : <><LogIn size={18} />Sign In</>}
        </h1>
        <p style={{ fontSize: 13, color: '#A1A1AA', marginBottom: 24 }}>
          {isRegister ? 'Create a new account to access the platform' : 'Enter your credentials to continue'}
        </p>

        {error && (
          <div style={{ background: '#EF444420', border: '1px solid #EF444440', borderRadius: 8, padding: '10px 14px', marginBottom: 16, fontSize: 13, color: '#EF4444' }}>
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div style={{ marginBottom: 14 }}>
            <label style={{ display: 'block', fontSize: 12, color: '#A1A1AA', marginBottom: 6 }}>Username</label>
            <input
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              autoFocus
              style={{ width: '100%', background: '#1A1A1A', border: '1px solid #2A2A2A', borderRadius: 8, padding: '10px 12px', color: '#FFFFFF', fontSize: 14, outline: 'none', boxSizing: 'border-box' }}
            />
          </div>

          <div style={{ marginBottom: 14 }}>
            <label style={{ display: 'block', fontSize: 12, color: '#A1A1AA', marginBottom: 6 }}>Password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              style={{ width: '100%', background: '#1A1A1A', border: '1px solid #2A2A2A', borderRadius: 8, padding: '10px 12px', color: '#FFFFFF', fontSize: 14, outline: 'none', boxSizing: 'border-box' }}
            />
          </div>

          {isRegister && (
            <div style={{ marginBottom: 14 }}>
              <label style={{ display: 'block', fontSize: 12, color: '#A1A1AA', marginBottom: 6 }}>Role</label>
              <select
                value={role}
                onChange={(e) => setRole(e.target.value)}
                style={{ width: '100%', background: '#1A1A1A', border: '1px solid #2A2A2A', borderRadius: 8, padding: '10px 12px', color: '#FFFFFF', fontSize: 14, outline: 'none', boxSizing: 'border-box' }}
              >
                <option value="User">User</option>
                <option value="Auditor">Auditor</option>
              </select>
            </div>
          )}

          <button
            type="submit"
            disabled={loading}
            style={{
              width: '100%',
              padding: '10px 16px',
              background: loading ? '#3B82F680' : '#3B82F6',
              color: '#FFFFFF',
              border: 'none',
              borderRadius: 8,
              fontSize: 14,
              fontWeight: 600,
              cursor: loading ? 'not-allowed' : 'pointer',
              marginBottom: 16,
            }}
          >
            {loading ? 'Please wait...' : isRegister ? 'Create Account' : 'Sign In'}
          </button>

          <div style={{ textAlign: 'center', fontSize: 13, color: '#A1A1AA' }}>
            {isRegister ? (
              <>Already have an account? <button type="button" onClick={() => { setIsRegister(false); setError(''); }} style={{ background: 'none', border: 'none', color: '#3B82F6', cursor: 'pointer', fontSize: 13 }}>Sign In</button></>
            ) : (
              <>Don't have an account? <button type="button" onClick={() => { setIsRegister(true); setError(''); }} style={{ background: 'none', border: 'none', color: '#3B82F6', cursor: 'pointer', fontSize: 13 }}>Create one</button></>
            )}
          </div>
        </form>

        <div style={{ marginTop: 20, padding: '12px 14px', background: '#1A1A1A', borderRadius: 8, fontSize: 12, color: '#A1A1AA' }}>
          <div style={{ fontWeight: 600, color: '#FFFFFF', marginBottom: 4 }}>Default Credentials</div>
          <div>admin / admin123</div>
        </div>
      </div>
    </div>
  );
}
