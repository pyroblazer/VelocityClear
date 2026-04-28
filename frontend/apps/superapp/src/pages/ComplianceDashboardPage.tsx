import { useQuery } from '@tanstack/react-query';
import { Shield, AlertTriangle, CheckCircle, Clock, Server } from 'lucide-react';
import { kycApi, amlApi, approvalApi, complaintApi, socApi } from '../lib/complianceApi';

const card = (accent = '#3B82F6') => ({
  background: '#141414',
  border: `1px solid ${accent}30`,
  borderRadius: 12,
  padding: 20,
  display: 'flex',
  flexDirection: 'column' as const,
  gap: 8,
});

function StatCard({ label, value, icon: Icon, accent }: { label: string; value: number | string; icon: any; accent: string }) {
  return (
    <div style={card(accent)}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <Icon size={16} style={{ color: accent }} />
        <span style={{ color: '#A1A1AA', fontSize: 12 }}>{label}</span>
      </div>
      <span style={{ color: '#FFF', fontSize: 28, fontWeight: 700 }}>{value}</span>
    </div>
  );
}

export default function ComplianceDashboardPage() {
  const { data: amlAlerts } = useQuery({ queryKey: ['aml-alerts'], queryFn: () => amlApi.listAlerts() });
  const { data: pendingApprovals } = useQuery({ queryKey: ['approvals-pending'], queryFn: () => approvalApi.list('PendingApproval') });
  const { data: complaints } = useQuery({ queryKey: ['complaints-all'], queryFn: () => complaintApi.list() });
  const { data: socDash } = useQuery({ queryKey: ['soc-dashboard'], queryFn: () => socApi.dashboard() });

  const openAlerts = Array.isArray(amlAlerts) ? amlAlerts.filter((a: any) => a.status === 'Open').length : 0;
  const pendingCount = Array.isArray(pendingApprovals) ? pendingApprovals.length : 0;
  const slaBreach = Array.isArray(complaints) ? complaints.filter((c: any) => c.slaBreach).length : 0;
  const openIncidents = (socDash as any)?.openIncidents ?? 0;

  return (
    <div style={{ padding: 24, display: 'flex', flexDirection: 'column', gap: 20 }}>
      <h1 style={{ color: '#FFF', fontSize: 22, fontWeight: 700, margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
        <Shield size={20} style={{ color: '#3B82F6' }} /> OJK Compliance Overview
      </h1>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
        <StatCard label="Open AML Alerts" value={openAlerts} icon={AlertTriangle} accent="#EF4444" />
        <StatCard label="Pending Approvals" value={pendingCount} icon={Clock} accent="#F59E0B" />
        <StatCard label="SLA Breaches" value={slaBreach} icon={CheckCircle} accent={slaBreach > 0 ? '#EF4444' : '#22C55E'} />
        <StatCard label="Open Security Incidents" value={openIncidents} icon={Server} accent="#8B5CF6" />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        {/* Recent AML Alerts */}
        <div style={{ background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 }}>
          <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Recent AML Alerts</h2>
          {Array.isArray(amlAlerts) && amlAlerts.slice(0, 5).map((a: any) => (
            <div key={a.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid #1A1A1A', fontSize: 12 }}>
              <span style={{ color: '#FFF' }}>{a.ruleTriggered}</span>
              <span style={{ color: { Low: '#A1A1AA', Medium: '#F59E0B', High: '#EF4444', Critical: '#DC2626' }[a.severity as string] ?? '#FFF' }}>
                {a.severity}
              </span>
            </div>
          ))}
          {(!amlAlerts || (amlAlerts as any[]).length === 0) && (
            <p style={{ color: '#A1A1AA', fontSize: 12 }}>No AML alerts.</p>
          )}
        </div>

        {/* Pending Approvals */}
        <div style={{ background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 }}>
          <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Pending Approvals</h2>
          {Array.isArray(pendingApprovals) && pendingApprovals.slice(0, 5).map((a: any) => (
            <div key={a.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid #1A1A1A', fontSize: 12 }}>
              <span style={{ color: '#FFF' }}>{a.approvalType}</span>
              <span style={{ color: '#F59E0B' }}>Requested by {a.requestedBy}</span>
            </div>
          ))}
          {(!pendingApprovals || (pendingApprovals as any[]).length === 0) && (
            <p style={{ color: '#A1A1AA', fontSize: 12 }}>No pending approvals.</p>
          )}
        </div>
      </div>

      {/* Complaints with SLA */}
      <div style={{ background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 }}>
        <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Customer Complaints (SLA)</h2>
        {Array.isArray(complaints) && complaints.slice(0, 6).map((c: any) => (
          <div key={c.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 0', borderBottom: '1px solid #1A1A1A', fontSize: 12 }}>
            <div>
              <span style={{ color: '#FFF', marginRight: 8 }}>{c.subject}</span>
              <span style={{ color: '#A1A1AA' }}>({c.category})</span>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <span style={{ color: '#A1A1AA' }}>{c.status}</span>
              {c.slaBreach && <span style={{ color: '#EF4444', fontWeight: 600 }}>SLA BREACH</span>}
            </div>
          </div>
        ))}
        {(!complaints || (complaints as any[]).length === 0) && (
          <p style={{ color: '#A1A1AA', fontSize: 12 }}>No complaints.</p>
        )}
      </div>
    </div>
  );
}
