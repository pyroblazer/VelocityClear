import { useState } from 'react';
import type { LucideIcon } from 'lucide-react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Shield, AlertTriangle, CheckCircle, Clock, Server, Zap } from 'lucide-react';
import type { AmlAlertResponse, ApprovalResponse, ComplaintResponse, SocDashboardResponse } from '../lib/complianceApi';
import { amlApi, approvalApi, complaintApi, socApi } from '../lib/complianceApi';

const card = (accent = '#3B82F6') => ({
  background: '#141414',
  border: `1px solid ${accent}30`,
  borderRadius: 12,
  padding: 20,
  display: 'flex',
  flexDirection: 'column' as const,
  gap: 8,
});

function StatCard({ label, value, icon: Icon, accent }: { label: string; value: number | string; icon: LucideIcon; accent: string }) {
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

const severityColor: Record<string, string> = { Low: '#A1A1AA', Medium: '#F59E0B', High: '#EF4444', Critical: '#DC2626' };

export default function ComplianceDashboardPage() {
  const queryClient = useQueryClient();
  const [simStatus, setSimStatus] = useState<string | null>(null);

  const { data: amlAlerts } = useQuery({ queryKey: ['aml-alerts'], queryFn: () => amlApi.listAlerts() });
  const { data: pendingApprovals } = useQuery({ queryKey: ['approvals-pending'], queryFn: () => approvalApi.list('PendingApproval') });
  const { data: complaints } = useQuery({ queryKey: ['complaints-all'], queryFn: () => complaintApi.list() });
  const { data: socDash } = useQuery({ queryKey: ['soc-dashboard'], queryFn: () => socApi.dashboard() });

  const alerts = amlAlerts ?? [];
  const approvals = pendingApprovals ?? [];
  const complaintList = complaints ?? [];
  const dash = socDash as SocDashboardResponse | undefined;

  const openAlerts = alerts.filter(a => a.status === 'Open').length;
  const pendingCount = approvals.length;
  const slaBreach = complaintList.filter(c => c.slaBreach).length;
  const openIncidents = dash?.openIncidents ?? 0;

  const simAml = useMutation({
    mutationFn: () => amlApi.fileSar({
      userId: 'sim-user',
      suspiciousAmount: 75000,
      reason: 'Structured deposits below reporting threshold',
      partiesInvolved: ['Account A', 'Account B'],
    }),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['aml-alerts'] }); setSimStatus('AML alert simulated'); setTimeout(() => setSimStatus(null), 3000); },
  });

  const simApproval = useMutation({
    mutationFn: () => approvalApi.create({
      approvalType: 'TransactionApproval',
      requestedBy: 'sim-user',
      description: 'Simulated high-value transfer requiring approval',
      transactionAmount: 250000,
    }),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['approvals-pending'] }); setSimStatus('Pending approval simulated'); setTimeout(() => setSimStatus(null), 3000); },
  });

  const simSlaBreach = useMutation({
    mutationFn: () => complaintApi.create({
      userId: 'sim-user',
      subject: 'SLA test complaint — auto-breach',
      category: 'ServiceQuality',
      description: 'Simulated complaint for SLA breach testing',
    }),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['complaints-all'] }); setSimStatus('SLA breach complaint simulated'); setTimeout(() => setSimStatus(null), 3000); },
  });

  const simIncident = useMutation({
    mutationFn: () => socApi.createIncident({
      title: 'Simulated critical security incident',
      severity: 'Critical',
      description: 'Unauthorized access attempt detected from external IP',
      detectedBy: 'SOC Simulation',
    }),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['soc-dashboard'] }); setSimStatus('Security incident simulated'); setTimeout(() => setSimStatus(null), 3000); },
  });

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

      {/* Simulation Controls */}
      <div style={{ background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 }}>
        <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px', display: 'flex', alignItems: 'center', gap: 8 }}>
          <Zap size={16} style={{ color: '#3B82F6' }} /> Simulation Controls
        </h2>
        {simStatus && (
          <div style={{ background: '#22C55E18', border: '1px solid #22C55E40', borderRadius: 8, padding: '8px 12px', marginBottom: 12, fontSize: 13, color: '#22C55E' }}>
            {simStatus}
          </div>
        )}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10 }}>
          <button
            onClick={() => simAml.mutate()}
            disabled={simAml.isPending}
            style={{ background: '#EF444420', border: '1px solid #EF444440', borderRadius: 8, padding: '8px 16px', color: '#EF4444', fontSize: 13, fontWeight: 600, cursor: simAml.isPending ? 'not-allowed' : 'pointer', opacity: simAml.isPending ? 0.6 : 1 }}
          >
            {simAml.isPending ? 'Running...' : 'Simulate AML Alert'}
          </button>
          <button
            onClick={() => simApproval.mutate()}
            disabled={simApproval.isPending}
            style={{ background: '#F59E0B20', border: '1px solid #F59E0B40', borderRadius: 8, padding: '8px 16px', color: '#F59E0B', fontSize: 13, fontWeight: 600, cursor: simApproval.isPending ? 'not-allowed' : 'pointer', opacity: simApproval.isPending ? 0.6 : 1 }}
          >
            {simApproval.isPending ? 'Running...' : 'Simulate Pending Approval'}
          </button>
          <button
            onClick={() => simSlaBreach.mutate()}
            disabled={simSlaBreach.isPending}
            style={{ background: '#22C55E20', border: '1px solid #22C55E40', borderRadius: 8, padding: '8px 16px', color: '#22C55E', fontSize: 13, fontWeight: 600, cursor: simSlaBreach.isPending ? 'not-allowed' : 'pointer', opacity: simSlaBreach.isPending ? 0.6 : 1 }}
          >
            {simSlaBreach.isPending ? 'Running...' : 'Simulate SLA Breach'}
          </button>
          <button
            onClick={() => simIncident.mutate()}
            disabled={simIncident.isPending}
            style={{ background: '#8B5CF620', border: '1px solid #8B5CF640', borderRadius: 8, padding: '8px 16px', color: '#8B5CF6', fontSize: 13, fontWeight: 600, cursor: simIncident.isPending ? 'not-allowed' : 'pointer', opacity: simIncident.isPending ? 0.6 : 1 }}
          >
            {simIncident.isPending ? 'Running...' : 'Simulate Security Incident'}
          </button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        {/* Recent AML Alerts */}
        <div style={{ background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 }}>
          <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Recent AML Alerts</h2>
          {alerts.slice(0, 5).map((a: AmlAlertResponse) => (
            <div key={a.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid #1A1A1A', fontSize: 12 }}>
              <span style={{ color: '#FFF' }}>{a.ruleTriggered}</span>
              <span style={{ color: severityColor[a.severity] ?? '#FFF' }}>
                {a.severity}
              </span>
            </div>
          ))}
          {alerts.length === 0 && (
            <p style={{ color: '#A1A1AA', fontSize: 12 }}>No AML alerts.</p>
          )}
        </div>

        {/* Pending Approvals */}
        <div style={{ background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 }}>
          <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Pending Approvals</h2>
          {approvals.slice(0, 5).map((a: ApprovalResponse) => (
            <div key={a.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '8px 0', borderBottom: '1px solid #1A1A1A', fontSize: 12 }}>
              <span style={{ color: '#FFF' }}>{a.approvalType}</span>
              <span style={{ color: '#F59E0B' }}>Requested by {a.requestedBy}</span>
            </div>
          ))}
          {approvals.length === 0 && (
            <p style={{ color: '#A1A1AA', fontSize: 12 }}>No pending approvals.</p>
          )}
        </div>
      </div>

      {/* Complaints with SLA */}
      <div style={{ background: '#141414', border: '1px solid #2A2A2A', borderRadius: 12, padding: 20 }}>
        <h2 style={{ color: '#FFF', fontSize: 15, margin: '0 0 16px' }}>Customer Complaints (SLA)</h2>
        {complaintList.slice(0, 6).map((c: ComplaintResponse) => (
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
        {complaintList.length === 0 && (
          <p style={{ color: '#A1A1AA', fontSize: 12 }}>No complaints.</p>
        )}
      </div>
    </div>
  );
}
