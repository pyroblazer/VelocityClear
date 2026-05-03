import { Routes, Route, Navigate, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { Zap, Shield, FileText, CreditCard, Activity, UserCheck, FileCheck, LayoutDashboard, LogOut, Settings } from 'lucide-react';
import TransactionsPage from './pages/TransactionsPage';
import AdminPage from './pages/AdminPage';
import RiskPage from './pages/RiskPage';
import AuditPage from './pages/AuditPage';
import CardOperationsPage from './pages/CardOperationsPage';
import KycPage from './pages/KycPage';
import ConsentPage from './pages/ConsentPage';
import ComplianceDashboardPage from './pages/ComplianceDashboardPage';
import SettingsPage from './pages/SettingsPage';
import LoginPage from './pages/LoginPage';
import ProtectedRoute from './components/ProtectedRoute';
import { useAuthStore } from './stores/authStore';

const navItems = [
  { to: '/transactions', label: 'Transactions', Icon: Zap, accent: '#3B82F6' },
  { to: '/admin', label: 'Admin', Icon: Shield, accent: '#A1A1AA' },
  { to: '/risk', label: 'Risk Monitor', Icon: Shield, accent: '#EF4444' },
  { to: '/audit', label: 'Audit Trail', Icon: FileText, accent: '#A855F7' },
  { to: '/cards', label: 'Card Operations', Icon: CreditCard, accent: '#22C55E' },
  { to: '/compliance', label: 'Compliance', Icon: LayoutDashboard, accent: '#3B82F6' },
  { to: '/kyc', label: 'KYC', Icon: UserCheck, accent: '#F59E0B' },
  { to: '/consent', label: 'Consent', Icon: FileCheck, accent: '#22C55E' },
  { to: '/settings', label: 'Settings', Icon: Settings, accent: '#A1A1AA' },
];

export default function App() {
  const logout = useAuthStore((s) => s.logout);
  const role = useAuthStore((s) => s.role);
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', background: '#0A0A0A' }}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<ProtectedRoute />}>
          <Route path="/" element={<AppShell logout={handleLogout} role={role} />}>
            <Route index element={<Navigate to="/transactions" replace />} />
            <Route path="/transactions" element={<TransactionsPage />} />
            <Route path="/admin" element={<AdminPage />} />
            <Route path="/risk" element={<RiskPage />} />
            <Route path="/audit" element={<AuditPage />} />
            <Route path="/cards" element={<CardOperationsPage />} />
            <Route path="/compliance" element={<ComplianceDashboardPage />} />
            <Route path="/kyc" element={<KycPage />} />
            <Route path="/consent" element={<ConsentPage />} />
            <Route path="/settings" element={<SettingsPage />} />
          </Route>
        </Route>
      </Routes>
    </div>
  );
}

function AppShell({ logout, role }: { logout: () => void; role: string | null }) {
  return (
    <>
      <header
        data-testid="app-header"
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          padding: '0 24px',
          height: 56,
          borderBottom: '1px solid #2A2A2A',
          background: '#141414',
          flexShrink: 0,
          zIndex: 10,
        }}
      >
        <Zap size={20} style={{ color: '#3B82F6' }} />
        <span style={{ fontWeight: 700, fontSize: 16, color: '#FFFFFF', letterSpacing: -0.3 }}>
          VelocityClear
        </span>
        <span style={{ marginLeft: 8, fontSize: 12, color: '#A1A1AA', borderLeft: '1px solid #2A2A2A', paddingLeft: 12 }}>
          Adaptive Real-Time Financial Platform
        </span>
        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 12 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: '#22C55E' }}>
            <span style={{ width: 7, height: 7, borderRadius: '50%', background: '#22C55E', animation: 'pulse-dot 2s infinite' }} />
            <Activity size={13} />
            Live
          </div>
          {role && (
            <span style={{ fontSize: 12, color: '#A1A1AA', borderLeft: '1px solid #2A2A2A', paddingLeft: 12 }}>
              {role}
            </span>
          )}
          <button
            onClick={logout}
            title="Sign out"
            style={{ background: 'none', border: 'none', color: '#A1A1AA', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 4, fontSize: 12 }}
          >
            <LogOut size={14} />
          </button>
        </div>
      </header>

      <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
        <nav
          data-testid="sidebar"
          style={{
            width: 200,
            background: '#141414',
            borderRight: '1px solid #2A2A2A',
            display: 'flex',
            flexDirection: 'column',
            padding: '16px 8px',
            gap: 4,
            flexShrink: 0,
          }}
        >
          {navItems.map(({ to, label, Icon, accent }) => (
            <NavLink
              key={to}
              to={to}
              data-testid={`nav-${to.slice(1)}`}
              style={({ isActive }) => ({
                display: 'flex',
                alignItems: 'center',
                gap: 10,
                padding: '9px 12px',
                borderRadius: 8,
                textDecoration: 'none',
                fontSize: 13,
                fontWeight: isActive ? 600 : 400,
                color: isActive ? '#FFFFFF' : '#A1A1AA',
                background: isActive ? `${accent}18` : 'transparent',
                border: `1px solid ${isActive ? `${accent}40` : 'transparent'}`,
                transition: 'all 0.15s',
              })}
            >
              {({ isActive }) => (
                <>
                  <Icon size={15} style={{ color: isActive ? accent : '#A1A1AA', flexShrink: 0 }} />
                  {label}
                </>
              )}
            </NavLink>
          ))}
        </nav>

        <main style={{ flex: 1, overflowY: 'auto' }}>
          <Outlet />
        </main>
      </div>
    </>
  );
}
