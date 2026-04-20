import { useState } from 'react';
import { Settings, Activity, Wifi, RefreshCw, Trash2, BarChart3, Server } from 'lucide-react';

interface ServiceStatus {
  name: string;
  port: number;
  status: 'healthy' | 'degraded' | 'down';
  uptime: string;
}

const initialServices: ServiceStatus[] = [
  { name: 'API Gateway', port: 5000, status: 'healthy', uptime: '99.9%' },
  { name: 'Transaction', port: 5001, status: 'healthy', uptime: '99.7%' },
  { name: 'Risk', port: 5002, status: 'healthy', uptime: '99.8%' },
  { name: 'Payment', port: 5003, status: 'degraded', uptime: '98.2%' },
  { name: 'Compliance', port: 5004, status: 'healthy', uptime: '99.9%' },
  { name: 'PIN Encryption', port: 5005, status: 'healthy', uptime: '99.9%' },
];

type EventBusBackend = 'InMemory' | 'Redis' | 'RabbitMQ' | 'Kafka';

const statusColor = { healthy: '#22C55E', degraded: '#F59E0B', down: '#EF4444' } as const;
const statusLabel = { healthy: 'Healthy', degraded: 'Degraded', down: 'Down' } as const;
const backendColor: Record<EventBusBackend, string> = { InMemory: '#A1A1AA', Redis: '#EF4444', RabbitMQ: '#F59E0B', Kafka: '#3B82F6' };

export default function AdminPage() {
  const [services] = useState<ServiceStatus[]>(initialServices);
  const [eventBusBackend] = useState<EventBusBackend>('InMemory');
  const [sseConnections] = useState(24);
  const [healthCheckRunning, setHealthCheckRunning] = useState(false);
  const [cacheCleared, setCacheCleared] = useState(false);
  const [showMetrics, setShowMetrics] = useState(false);

  const handleHealthCheck = () => { setHealthCheckRunning(true); setTimeout(() => setHealthCheckRunning(false), 2000); };
  const handleClearCache = () => { setCacheCleared(true); setTimeout(() => setCacheCleared(false), 1500); };

  return (
    <div className="min-h-screen bg-[#0A0A0A] text-white p-6">
      <header className="flex items-center gap-3 mb-8 border-b border-[#2A2A2A] pb-4">
        <Settings className="w-6 h-6 text-[#3B82F6]" />
        <h1 className="text-2xl font-bold">Admin Control Panel</h1>
        <span className="ml-auto text-[#A1A1AA] text-sm">Last updated: just now</span>
      </header>

      {/* Service status */}
      <section className="mb-8">
        <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
          <Server className="w-5 h-5 text-[#3B82F6]" />
          System Status
        </h2>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          {services.map((svc) => (
            <div key={svc.name} className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4 hover:border-[#3B82F6]/50 transition-colors">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-medium text-[#A1A1AA]">{svc.name}</span>
                <div className="w-2 h-2 rounded-full" style={{ background: statusColor[svc.status] }} />
              </div>
              <div className="text-base font-semibold">:{svc.port}</div>
              <div className="text-xs text-[#A1A1AA] mt-1">{statusLabel[svc.status]} · {svc.uptime}</div>
            </div>
          ))}
        </div>
      </section>

      {/* Event bus + SSE */}
      <section className="mb-8 grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
          <h3 className="text-base font-semibold mb-3 flex items-center gap-2">
            <Wifi className="w-4 h-4 text-[#3B82F6]" />
            Event Bus
          </h3>
          <div className="flex items-center gap-3 mb-3">
            <div className="w-3 h-3 rounded-full" style={{ background: backendColor[eventBusBackend] }} />
            <span className="text-lg font-semibold">{eventBusBackend}</span>
          </div>
          <div className="flex gap-2 flex-wrap">
            {(['InMemory', 'Redis', 'RabbitMQ', 'Kafka'] as EventBusBackend[]).map((b) => (
              <span key={b} className={`text-xs px-2 py-1 rounded border ${b === eventBusBackend ? 'border-[#3B82F6] text-[#3B82F6] bg-[#3B82F6]/10' : 'border-[#2A2A2A] text-[#A1A1AA]'}`}>{b}</span>
            ))}
          </div>
        </div>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
          <h3 className="text-base font-semibold mb-3 flex items-center gap-2">
            <Activity className="w-4 h-4 text-[#3B82F6]" />
            SSE Connections
          </h3>
          <div className="flex items-baseline gap-2">
            <span className="text-4xl font-bold">{sseConnections}</span>
            <span className="text-[#A1A1AA]">active streams</span>
          </div>
        </div>
      </section>

      {/* Quick actions */}
      <section className="mb-8">
        <h2 className="text-lg font-semibold mb-4">Quick Actions</h2>
        <div className="flex flex-wrap gap-3">
          <button onClick={handleHealthCheck} disabled={healthCheckRunning} className="flex items-center gap-2 px-5 py-2.5 bg-[#3B82F6] hover:bg-[#3B82F6]/80 disabled:opacity-50 rounded-lg font-medium transition-colors" data-testid="health-check-btn">
            <RefreshCw className={`w-4 h-4 ${healthCheckRunning ? 'animate-spin' : ''}`} />
            {healthCheckRunning ? 'Running...' : 'Run Health Check'}
          </button>
          <button onClick={handleClearCache} className="flex items-center gap-2 px-5 py-2.5 bg-[#1A1A1A] border border-[#2A2A2A] hover:border-[#EF4444]/50 rounded-lg font-medium transition-colors" data-testid="clear-cache-btn">
            <Trash2 className="w-4 h-4 text-[#EF4444]" />
            {cacheCleared ? 'Cache Cleared!' : 'Clear Cache'}
          </button>
          <button onClick={() => setShowMetrics(!showMetrics)} className="flex items-center gap-2 px-5 py-2.5 bg-[#1A1A1A] border border-[#2A2A2A] hover:border-[#3B82F6]/50 rounded-lg font-medium transition-colors" data-testid="metrics-btn">
            <BarChart3 className="w-4 h-4 text-[#3B82F6]" />
            View Metrics
          </button>
        </div>
        {showMetrics && (
          <div className="mt-4 bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div><span className="text-sm text-[#A1A1AA]">CPU Usage</span><div className="text-2xl font-bold text-[#22C55E]">23%</div></div>
              <div><span className="text-sm text-[#A1A1AA]">Memory</span><div className="text-2xl font-bold text-[#3B82F6]">4.2 GB</div></div>
              <div><span className="text-sm text-[#A1A1AA]">Requests/sec</span><div className="text-2xl font-bold text-[#F59E0B]">1,247</div></div>
              <div><span className="text-sm text-[#A1A1AA]">Avg Latency</span><div className="text-2xl font-bold text-[#22C55E]">42ms</div></div>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}
