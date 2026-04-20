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
];

type EventBusBackend = 'InMemory' | 'Redis' | 'RabbitMQ' | 'Kafka';

const statusColor = {
  healthy: 'bg-[#22C55E]',
  degraded: 'bg-[#F59E0B]',
  down: 'bg-[#EF4444]',
} as const;

const statusLabel = {
  healthy: 'Healthy',
  degraded: 'Degraded',
  down: 'Down',
} as const;

const backendIndicator: Record<EventBusBackend, string> = {
  InMemory: 'bg-[#A1A1AA]',
  Redis: 'bg-[#EF4444]',
  RabbitMQ: 'bg-[#F59E0B]',
  Kafka: 'bg-[#3B82F6]',
};

function App() {
  const [services] = useState<ServiceStatus[]>(initialServices);
  const [eventBusBackend] = useState<EventBusBackend>('InMemory');
  const [sseConnections] = useState(24);
  const [healthCheckRunning, setHealthCheckRunning] = useState(false);
  const [cacheCleared, setCacheCleared] = useState(false);
  const [showMetrics, setShowMetrics] = useState(false);

  const handleHealthCheck = () => {
    setHealthCheckRunning(true);
    setTimeout(() => setHealthCheckRunning(false), 2000);
  };

  const handleClearCache = () => {
    setCacheCleared(true);
    setTimeout(() => setCacheCleared(false), 1500);
  };

  return (
    <div className="min-h-screen bg-[#0A0A0A] text-white p-6">
      {/* Header */}
      <header className="flex items-center gap-3 mb-8 border-b border-[#2A2A2A] pb-4">
        <Settings className="w-8 h-8 text-[#3B82F6]" />
        <h1 className="text-3xl font-bold">Admin Control Panel</h1>
        <span className="ml-auto text-[#A1A1AA] text-sm">Last updated: just now</span>
      </header>

      {/* System Status Section */}
      <section className="mb-8">
        <h2 className="text-xl font-semibold mb-4 flex items-center gap-2">
          <Server className="w-5 h-5 text-[#3B82F6]" />
          System Status
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4">
          {services.map((service) => (
            <div
              key={service.name}
              className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4 hover:border-[#3B82F6]/50 transition-colors"
            >
              <div className="flex items-center justify-between mb-3">
                <span className="text-sm font-medium text-[#A1A1AA]">{service.name}</span>
                <div className="flex items-center gap-2">
                  <div className={`w-2.5 h-2.5 rounded-full ${statusColor[service.status]}`} />
                  <span className="text-xs text-[#A1A1AA]">{statusLabel[service.status]}</span>
                </div>
              </div>
              <div className="text-lg font-semibold">:{service.port}</div>
              <div className="text-xs text-[#A1A1AA] mt-1">Uptime: {service.uptime}</div>
            </div>
          ))}
        </div>
      </section>

      {/* Event Bus & SSE Row */}
      <section className="mb-8 grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Event Bus Status */}
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
          <h3 className="text-lg font-semibold mb-3 flex items-center gap-2">
            <Wifi className="w-5 h-5 text-[#3B82F6]" />
            Event Bus Status
          </h3>
          <div className="flex items-center gap-3 mb-3">
            <div className={`w-3 h-3 rounded-full ${backendIndicator[eventBusBackend]}`} />
            <span className="text-xl font-semibold">{eventBusBackend}</span>
          </div>
          <div className="text-sm text-[#A1A1AA]">
            Current backend transport layer for event-driven communication.
          </div>
          <div className="mt-3 flex gap-2">
            {(['InMemory', 'Redis', 'RabbitMQ', 'Kafka'] as EventBusBackend[]).map((backend) => (
              <span
                key={backend}
                className={`text-xs px-2 py-1 rounded border transition-colors ${
                  backend === eventBusBackend
                    ? 'border-[#3B82F6] text-[#3B82F6] bg-[#3B82F6]/10'
                    : 'border-[#2A2A2A] text-[#A1A1AA] bg-transparent'
                }`}
              >
                {backend}
              </span>
            ))}
          </div>
        </div>

        {/* SSE Connections */}
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
          <h3 className="text-lg font-semibold mb-3 flex items-center gap-2">
            <Activity className="w-5 h-5 text-[#3B82F6]" />
            SSE Connections
          </h3>
          <div className="flex items-baseline gap-2">
            <span className="text-4xl font-bold">{sseConnections}</span>
            <span className="text-[#A1A1AA]">active streams</span>
          </div>
          <div className="mt-3 grid grid-cols-2 gap-2 text-sm">
            <div className="bg-[#0A0A0A] rounded p-2">
              <span className="text-[#A1A1AA]">Transaction Events</span>
              <div className="font-semibold">8</div>
            </div>
            <div className="bg-[#0A0A0A] rounded p-2">
              <span className="text-[#A1A1AA]">Risk Events</span>
              <div className="font-semibold">6</div>
            </div>
            <div className="bg-[#0A0A0A] rounded p-2">
              <span className="text-[#A1A1AA]">Audit Events</span>
              <div className="font-semibold">7</div>
            </div>
            <div className="bg-[#0A0A0A] rounded p-2">
              <span className="text-[#A1A1AA]">Payment Events</span>
              <div className="font-semibold">3</div>
            </div>
          </div>
        </div>
      </section>

      {/* Quick Actions */}
      <section className="mb-8">
        <h2 className="text-xl font-semibold mb-4">Quick Actions</h2>
        <div className="flex flex-wrap gap-3">
          <button
            onClick={handleHealthCheck}
            disabled={healthCheckRunning}
            className="flex items-center gap-2 px-5 py-2.5 bg-[#3B82F6] hover:bg-[#3B82F6]/80 disabled:opacity-50 rounded-lg font-medium transition-colors"
          >
            <RefreshCw className={`w-4 h-4 ${healthCheckRunning ? 'animate-spin' : ''}`} />
            {healthCheckRunning ? 'Running...' : 'Run Health Check'}
          </button>
          <button
            onClick={handleClearCache}
            className="flex items-center gap-2 px-5 py-2.5 bg-[#1A1A1A] border border-[#2A2A2A] hover:border-[#EF4444]/50 rounded-lg font-medium transition-colors"
          >
            <Trash2 className="w-4 h-4 text-[#EF4444]" />
            {cacheCleared ? 'Cache Cleared!' : 'Clear Cache'}
          </button>
          <button
            onClick={() => setShowMetrics(!showMetrics)}
            className="flex items-center gap-2 px-5 py-2.5 bg-[#1A1A1A] border border-[#2A2A2A] hover:border-[#3B82F6]/50 rounded-lg font-medium transition-colors"
          >
            <BarChart3 className="w-4 h-4 text-[#3B82F6]" />
            View Metrics
          </button>
        </div>

        {showMetrics && (
          <div className="mt-4 bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
            <h3 className="text-lg font-semibold mb-3">System Metrics</h3>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div>
                <span className="text-sm text-[#A1A1AA]">CPU Usage</span>
                <div className="text-2xl font-bold text-[#22C55E]">23%</div>
              </div>
              <div>
                <span className="text-sm text-[#A1A1AA]">Memory</span>
                <div className="text-2xl font-bold text-[#3B82F6]">4.2 GB</div>
              </div>
              <div>
                <span className="text-sm text-[#A1A1AA]">Requests/sec</span>
                <div className="text-2xl font-bold text-[#F59E0B]">1,247</div>
              </div>
              <div>
                <span className="text-sm text-[#A1A1AA]">Avg Latency</span>
                <div className="text-2xl font-bold text-[#22C55E]">42ms</div>
              </div>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}

export default App;
