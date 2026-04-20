import { useState } from 'react';
import { FileText, Shield, Link, Filter, Search } from 'lucide-react';

interface AuditLog {
  id: string;
  type: 'TransactionCreated' | 'RiskEvaluated' | 'PaymentAuthorized' | 'AuditLogged';
  payload: Record<string, unknown>;
  hash: string;
  prevHash: string | null;
  time: string;
}

const auditLogs: AuditLog[] = [
  { id: 'audit_001', type: 'TransactionCreated', payload: { txId: 'TXN-2024-0847', amount: 5000 }, hash: 'A1B2C3...F4E5', prevHash: null, time: '14:32:18' },
  { id: 'audit_002', type: 'RiskEvaluated', payload: { txId: 'TXN-2024-0847', score: 45 }, hash: 'D6E7F8...A9B0', prevHash: 'A1B2C3...F4E5', time: '14:32:19' },
  { id: 'audit_003', type: 'PaymentAuthorized', payload: { txId: 'TXN-2024-0847', authorized: true }, hash: 'C1D2E3...F4A5', prevHash: 'D6E7F8...A9B0', time: '14:32:20' },
  { id: 'audit_004', type: 'AuditLogged', payload: { txId: 'TXN-2024-0847', action: 'record' }, hash: 'E6F7A8...B9C0', prevHash: 'C1D2E3...F4A5', time: '14:32:21' },
  { id: 'audit_005', type: 'TransactionCreated', payload: { txId: 'TXN-2024-0848', amount: 250 }, hash: 'B5C6D7...E8F9', prevHash: 'E6F7A8...B9C0', time: '14:33:00' },
  { id: 'audit_006', type: 'RiskEvaluated', payload: { txId: 'TXN-2024-0848', score: 12 }, hash: 'F1A2B3...C4D5', prevHash: 'B5C6D7...E8F9', time: '14:33:01' },
  { id: 'audit_007', type: 'PaymentAuthorized', payload: { txId: 'TXN-2024-0848', authorized: true }, hash: 'G2H3I4...J5K6', prevHash: 'F1A2B3...C4D5', time: '14:33:02' },
];

const typeColor: Record<string, string> = {
  TransactionCreated: '#3B82F6',
  RiskEvaluated: '#F59E0B',
  PaymentAuthorized: '#22C55E',
  AuditLogged: '#A855F7',
};

const filters = ['All', 'TransactionCreated', 'RiskEvaluated', 'PaymentAuthorized', 'AuditLogged'] as const;

export default function AuditPage() {
  const [activeFilter, setActiveFilter] = useState<string>('All');
  const [searchQuery, setSearchQuery] = useState('');

  const filtered = auditLogs.filter((log) => {
    const matchesFilter = activeFilter === 'All' || log.type === activeFilter;
    const matchesSearch = searchQuery === '' || log.hash.toLowerCase().includes(searchQuery.toLowerCase()) || log.type.toLowerCase().includes(searchQuery.toLowerCase()) || JSON.stringify(log.payload).toLowerCase().includes(searchQuery.toLowerCase());
    return matchesFilter && matchesSearch;
  });

  return (
    <div className="bg-[#0A0A0A] text-white p-6">
      <header className="flex items-center gap-3 mb-6 border-b border-[#2A2A2A] pb-4">
        <FileText className="w-6 h-6 text-[#A855F7]" />
        <h1 className="text-2xl font-bold">Audit Trail</h1>
        <div className="ml-auto flex items-center gap-2 text-sm text-[#A1A1AA]"><Shield className="w-4 h-4 text-[#22C55E]" />Chain Verified</div>
      </header>

      <section className="mb-6 grid grid-cols-3 gap-4">
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4"><div className="text-sm text-[#A1A1AA] mb-1">Total Events</div><div className="text-2xl font-bold">{auditLogs.length}</div></div>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4">
          <div className="text-sm text-[#A1A1AA] mb-2">Chain Integrity</div>
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2"><Shield className="w-4 h-4 text-[#22C55E]" /><span className="text-xl font-bold text-[#22C55E]">{auditLogs.length}</span><span className="text-sm text-[#22C55E]">Verified</span></div>
            <div className="flex items-center gap-2"><span className="text-xl font-bold text-[#EF4444]">0</span><span className="text-sm text-[#EF4444]">Tampered</span></div>
          </div>
        </div>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4"><div className="text-sm text-[#A1A1AA] mb-1">Events Today</div><div className="text-2xl font-bold">{auditLogs.length}</div></div>
      </section>

      {/* Filters + search */}
      <section className="mb-4">
        <div className="flex flex-wrap items-center gap-3 mb-3">
          <Filter className="w-4 h-4 text-[#A1A1AA]" />
          {filters.map((f) => (
            <button key={f} onClick={() => setActiveFilter(f)} className={`text-xs px-3 py-1.5 rounded-lg border font-medium transition-colors ${activeFilter === f ? 'border-[#3B82F6] text-[#3B82F6] bg-[#3B82F6]/10' : 'border-[#2A2A2A] text-[#A1A1AA] bg-[#1A1A1A] hover:border-[#A1A1AA]/30'}`}>
              {f}
            </button>
          ))}
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
          <div className="relative pl-8">
            <div className="absolute left-3 top-3 bottom-3 w-0.5 bg-[#2A2A2A]" />
            {filtered.length === 0 && <div className="text-center text-[#A1A1AA] py-8">No events match the current filter.</div>}
            {filtered.map((log, index) => {
              const color = typeColor[log.type] || '#A1A1AA';
              return (
                <div key={log.id} className="relative pb-6 last:pb-0">
                  <div className="absolute -left-8 top-1 w-6 h-6 rounded-full flex items-center justify-center border-2" style={{ borderColor: color, backgroundColor: `${color}20` }} />
                  <div className="bg-[#0A0A0A] border border-[#2A2A2A] rounded-lg p-4">
                    <div className="flex items-center gap-3 mb-2 flex-wrap">
                      <span className="text-xs font-semibold px-2.5 py-1 rounded-md" style={{ backgroundColor: `${color}20`, color }}>{log.type}</span>
                      <span className="text-xs text-[#A1A1AA]">{log.time}</span>
                    </div>
                    <div className="flex items-center gap-3 text-xs font-mono mb-2 flex-wrap">
                      <div className="flex items-center gap-1.5"><span className="text-[#A1A1AA]">Hash:</span><span className="text-[#3B82F6]">{log.hash}</span></div>
                      {log.prevHash && <div className="flex items-center gap-1.5"><span className="text-[#A1A1AA]">Prev:</span><span className="text-[#A1A1AA]/70">{log.prevHash}</span></div>}
                    </div>
                    <div className="text-xs text-[#A1A1AA]/80 font-mono">{JSON.stringify(log.payload)}</div>
                  </div>
                  {index < filtered.length - 1 && <div className="flex items-center justify-center py-1"><div className="w-0.5 h-4 bg-[#2A2A2A]" /></div>}
                </div>
              );
            })}
          </div>
        </div>
      </section>

      {/* Chain link viz */}
      <section>
        <h2 className="text-base font-semibold mb-4 flex items-center gap-2"><Link className="w-4 h-4 text-[#3B82F6]" />Chain Link Visualization</h2>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4 overflow-x-auto">
          <div className="flex items-center gap-2 min-w-max">
            {filtered.map((log, index) => {
              const color = typeColor[log.type] || '#A1A1AA';
              return (
                <div key={log.id} className="flex items-center gap-2">
                  <div className="flex flex-col items-center gap-1 px-3 py-2 rounded-lg border min-w-[90px]" style={{ borderColor: `${color}40`, backgroundColor: `${color}10` }}>
                    <span className="text-[10px] font-mono text-[#A1A1AA] truncate w-full text-center">{log.hash}</span>
                    <span className="text-[9px] font-semibold" style={{ color }}>{log.type}</span>
                  </div>
                  {index < filtered.length - 1 && <svg className="w-6 h-4 shrink-0" viewBox="0 0 24 16"><line x1="0" y1="8" x2="16" y2="8" stroke="#2A2A2A" strokeWidth="2" /><polygon points="16,3 24,8 16,13" fill="#2A2A2A" /></svg>}
                </div>
              );
            })}
          </div>
        </div>
      </section>
    </div>
  );
}
