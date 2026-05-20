import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { FileText, Shield, Link, Filter, Search, CheckCircle, XCircle } from 'lucide-react';
import { auditApi } from '../lib/gatewayApi';

interface AuditLog {
  id: string;
  eventType: string;
  payload: string;
  hash: string;
  previousHash: string | null;
  createdAt: string;
}

const typeColor: Record<string, string> = {
  TransactionCreated: '#3B82F6',
  RiskEvaluated: '#F59E0B',
  PaymentAuthorized: '#22C55E',
  AuditLogged: '#A855F7',
  PinVerified: '#8B5CF6',
  ConsentGranted: '#22C55E',
  ConsentWithdrawn: '#EF4444',
  KycStatusChanged: '#3B82F6',
  AmlAlertTriggered: '#EF4444',
};

const filters = ['All', 'TransactionCreated', 'RiskEvaluated', 'PaymentAuthorized', 'AuditLogged', 'PinVerified'] as const;

export default function AuditPage() {
  const [activeFilter, setActiveFilter] = useState<string>('All');
  const [searchQuery, setSearchQuery] = useState('');

  const { data: auditLogs = [], isLoading, isError } = useQuery({
    queryKey: ['audit-logs'],
    queryFn: async () => {
      const raw = await auditApi.list(1, 50);
      return (Array.isArray(raw) ? raw : []) as AuditLog[];
    },
    refetchInterval: 30000,
  });

  const { data: stats } = useQuery({
    queryKey: ['audit-stats'],
    queryFn: auditApi.getStats,
  });

  const { data: verifyResult, refetch: runVerify, isFetching: verifying } = useQuery({
    queryKey: ['audit-verify'],
    queryFn: auditApi.verify,
    enabled: false,
  });

  const filtered = auditLogs.filter((log) => {
    const matchesFilter = activeFilter === 'All' || log.eventType === activeFilter;
    const q = searchQuery.toLowerCase();
    const matchesSearch = q === '' ||
      log.hash?.toLowerCase().includes(q) ||
      log.eventType?.toLowerCase().includes(q) ||
      log.payload?.toLowerCase().includes(q);
    return matchesFilter && matchesSearch;
  });

  const parseTime = (iso: string) => {
    try { return new Date(iso).toLocaleTimeString(); }
    catch { return iso; }
  };

  const parsePayload = (payload: string) => {
    try { return JSON.parse(payload); }
    catch { return payload; }
  };

  return (
    <div className="bg-[#0A0A0A] text-white p-6">
      <header className="flex items-center gap-3 mb-6 border-b border-[#2A2A2A] pb-4">
        <FileText className="w-6 h-6 text-[#A855F7]" />
        <h1 className="text-2xl font-bold">Audit Trail</h1>
        <div className="ml-auto flex items-center gap-2 text-sm text-[#A1A1AA]">
          <Shield className="w-4 h-4 text-[#22C55E]" />
          {verifying ? 'Verifying...' : verifyResult?.isValid ? 'Chain Verified' : 'Chain Integrity'}
        </div>
      </header>

      {isError && (
        <div className="mb-6 bg-[#EF4444]/10 border border-[#EF4444]/30 rounded-lg p-4 text-sm text-[#EF4444]">
          Failed to load audit logs. Make sure the Compliance Service is running.
        </div>
      )}

      <section className="mb-6 grid grid-cols-3 gap-4">
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4">
          <div className="text-sm text-[#A1A1AA] mb-1">Total Events</div>
          <div className="text-2xl font-bold">{stats?.totalEvents ?? auditLogs.length}</div>
        </div>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4">
          <div className="text-sm text-[#A1A1AA] mb-2">Chain Integrity</div>
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2">
              {verifyResult ? (
                verifyResult.isValid
                  ? <><CheckCircle className="w-4 h-4 text-[#22C55E]" /><span className="text-xl font-bold text-[#22C55E]">{verifyResult.verifiedLogs}</span><span className="text-sm text-[#22C55E]">Verified</span></>
                  : <><XCircle className="w-4 h-4 text-[#EF4444]" /><span className="text-xl font-bold text-[#EF4444]">Tampered</span></>
              ) : (
                <span className="text-sm text-[#A1A1AA]">Click "Verify Chain" below</span>
              )}
            </div>
          </div>
        </div>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4">
          <div className="text-sm text-[#A1A1AA] mb-1">Event Types</div>
          <div className="text-2xl font-bold">{stats?.eventsByType ? Object.keys(stats.eventsByType).length : '-'}</div>
        </div>
      </section>

      {/* Filters + search + verify */}
      <section className="mb-4">
        <div className="flex flex-wrap items-center gap-3 mb-3">
          <Filter className="w-4 h-4 text-[#A1A1AA]" />
          {filters.map((f) => (
            <button key={f} onClick={() => setActiveFilter(f)} className={`text-xs px-3 py-1.5 rounded-lg border font-medium transition-colors ${activeFilter === f ? 'border-[#3B82F6] text-[#3B82F6] bg-[#3B82F6]/10' : 'border-[#2A2A2A] text-[#A1A1AA] bg-[#1A1A1A] hover:border-[#A1A1AA]/30'}`}>
              {f}
            </button>
          ))}
          <button
            onClick={() => runVerify()}
            disabled={verifying}
            className="text-xs px-3 py-1.5 rounded-lg border border-[#22C55E]/40 text-[#22C55E] bg-[#22C55E]/10 hover:bg-[#22C55E]/20 transition-colors ml-auto"
          >
            {verifying ? 'Verifying...' : 'Verify Chain'}
          </button>
        </div>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#A1A1AA]" />
          <input type="text" placeholder="Search by hash, type, or payload..." value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} className="w-full bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg py-2 pl-10 pr-4 text-sm text-white placeholder-[#A1A1AA] focus:outline-none focus:border-[#3B82F6]/50 transition-colors" data-testid="audit-search" />
        </div>
      </section>

      {/* Timeline */}
      <section className="mb-6">
        <h2 className="text-base font-semibold mb-4 flex items-center gap-2"><Link className="w-4 h-4 text-[#3B82F6]" />Hash Chain Timeline</h2>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-6">
          {isLoading ? (
            <div className="text-center text-[#A1A1AA] py-8">Loading audit logs...</div>
          ) : (
            <div className="relative pl-8">
              <div className="absolute left-3 top-3 bottom-3 w-0.5 bg-[#2A2A2A]" />
              {filtered.length === 0 && <div className="text-center text-[#A1A1AA] py-8">No events match the current filter.</div>}
              {filtered.map((log, index) => {
                const color = typeColor[log.eventType] || '#A1A1AA';
                const payload = parsePayload(log.payload);
                return (
                  <div key={log.id} className="relative pb-6 last:pb-0">
                    <div className="absolute -left-8 top-1 w-6 h-6 rounded-full flex items-center justify-center border-2" style={{ borderColor: color, backgroundColor: `${color}20` }} />
                    <div className="bg-[#0A0A0A] border border-[#2A2A2A] rounded-lg p-4">
                      <div className="flex items-center gap-3 mb-2 flex-wrap">
                        <span className="text-xs font-semibold px-2.5 py-1 rounded-md" style={{ backgroundColor: `${color}20`, color }}>{log.eventType}</span>
                        <span className="text-xs text-[#A1A1AA]">{parseTime(log.createdAt)}</span>
                      </div>
                      <div className="flex items-center gap-3 text-xs font-mono mb-2 flex-wrap">
                        <div className="flex items-center gap-1.5"><span className="text-[#A1A1AA]">Hash:</span><span className="text-[#3B82F6]">{log.hash?.slice(0, 16)}...</span></div>
                        {log.previousHash && <div className="flex items-center gap-1.5"><span className="text-[#A1A1AA]">Prev:</span><span className="text-[#A1A1AA]/70">{log.previousHash.slice(0, 16)}...</span></div>}
                      </div>
                      <div className="text-xs text-[#A1A1AA]/80 font-mono">{typeof payload === 'string' ? payload : JSON.stringify(payload)}</div>
                    </div>
                    {index < filtered.length - 1 && <div className="flex items-center justify-center py-1"><div className="w-0.5 h-4 bg-[#2A2A2A]" /></div>}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </section>

      {/* Chain link viz */}
      <section>
        <h2 className="text-base font-semibold mb-4 flex items-center gap-2"><Link className="w-4 h-4 text-[#3B82F6]" />Chain Link Visualization</h2>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4 overflow-x-auto">
          <div className="flex items-center gap-2 min-w-max">
            {filtered.slice(0, 12).map((log, index) => {
              const color = typeColor[log.eventType] || '#A1A1AA';
              return (
                <div key={log.id} className="flex items-center gap-2">
                  <div className="flex flex-col items-center gap-1 px-3 py-2 rounded-lg border min-w-[90px]" style={{ borderColor: `${color}40`, backgroundColor: `${color}10` }}>
                    <span className="text-[10px] font-mono text-[#A1A1AA] truncate w-full text-center">{log.hash?.slice(0, 12)}...</span>
                    <span className="text-[9px] font-semibold" style={{ color }}>{log.eventType.length > 14 ? log.eventType.slice(0, 12) + '...' : log.eventType}</span>
                  </div>
                  {index < Math.min(filtered.length, 12) - 1 && <svg className="w-6 h-4 shrink-0" viewBox="0 0 24 16"><line x1="0" y1="8" x2="16" y2="8" stroke="#2A2A2A" strokeWidth="2" /><polygon points="16,3 24,8 16,13" fill="#2A2A2A" /></svg>}
                </div>
              );
            })}
          </div>
        </div>
      </section>
    </div>
  );
}
