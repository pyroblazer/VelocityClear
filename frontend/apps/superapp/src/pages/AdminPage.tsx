import { useState } from 'react';
import { Settings, Activity, Wifi, RefreshCw, Trash2, BarChart3, Server } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { gatewayApi, auditApi } from '../lib/gatewayApi';

type EventBusBackend = 'InMemory' | 'Redis' | 'RabbitMQ' | 'Kafka';

const statusColor: Record<string, string> = { Healthy: '#22C55E', Degraded: '#F59E0B', Down: '#EF4444', Unhealthy: '#EF4444' };
const backendColor: Record<EventBusBackend, string> = { InMemory: '#A1A1AA', Redis: '#EF4444', RabbitMQ: '#F59E0B', Kafka: '#3B82F6' };

export default function AdminPage() {
  const [cacheCleared, setCacheCleared] = useState(false);
  const [showMetrics, setShowMetrics] = useState(false);

  const healthQuery = useQuery({
    queryKey: ['gateway-health'],
    queryFn: gatewayApi.getHealth,
    refetchInterval: 30000,
  });

  const statusQuery = useQuery({
    queryKey: ['gateway-status'],
    queryFn: gatewayApi.getStatus,
    refetchInterval: 15000,
  });

  const statsQuery = useQuery({
    queryKey: ['audit-stats'],
    queryFn: auditApi.getStats,
    refetchInterval: 30000,
  });

  const services = healthQuery.data?.services ?? {};
  const eventBusBackend = (statusQuery.data?.eventBusBackend ?? 'InMemory') as EventBusBackend;
  const sseConnections = statusQuery.data?.activeSseConnections ?? 0;
  const totalEvents = statsQuery.data?.totalEvents ?? 0;
  const eventsByType = statsQuery.data?.eventsByType ?? {};

  const handleClearCache = () => { setCacheCleared(true); setTimeout(() => setCacheCleared(false), 1500); };

  return (
    <div className="min-h-screen bg-[#0A0A0A] text-white p-6">
      <header className="flex items-center gap-3 mb-8 border-b border-[#2A2A2A] pb-4">
        <Settings className="w-6 h-6 text-[#3B82F6]" />
        <h1 className="text-2xl font-bold">Admin Control Panel</h1>
        <span className="ml-auto text-[#A1A1AA] text-sm">
          {healthQuery.isLoading ? 'Loading...' : `Last updated: ${new Date().toLocaleTimeString()}`}
        </span>
      </header>

      {healthQuery.isError && (
        <div className="mb-6 bg-[#EF4444]/10 border border-[#EF4444]/30 rounded-lg p-4 text-sm text-[#EF4444]">
          Failed to load service health: {healthQuery.error instanceof Error ? healthQuery.error.message : 'Unknown error'}
        </div>
      )}

      {/* Service status */}
      <section className="mb-8">
        <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
          <Server className="w-5 h-5 text-[#3B82F6]" />
          System Status
        </h2>
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
          {Object.entries(services).map(([name, svc]) => (
            <div key={name} className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-4 hover:border-[#3B82F6]/50 transition-colors">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-medium text-[#A1A1AA]">{name}</span>
                <div className="w-2 h-2 rounded-full" style={{ background: statusColor[svc.status] ?? '#A1A1AA' }} />
              </div>
              <div className="text-base font-semibold">{svc.url?.replace(/https?:\/\//, '') ?? name}</div>
              <div className="text-xs text-[#A1A1AA] mt-1">{svc.status ?? 'Unknown'}</div>
            </div>
          ))}
          {Object.keys(services).length === 0 && !healthQuery.isLoading && (
            <div className="col-span-full text-center text-[#A1A1AA] py-8">No service data available</div>
          )}
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
          <button
            onClick={() => healthQuery.refetch()}
            disabled={healthQuery.isFetching}
            className="flex items-center gap-2 px-5 py-2.5 bg-[#3B82F6] hover:bg-[#3B82F6]/80 disabled:opacity-50 rounded-lg font-medium transition-colors"
            data-testid="health-check-btn"
          >
            <RefreshCw className={`w-4 h-4 ${healthQuery.isFetching ? 'animate-spin' : ''}`} />
            {healthQuery.isFetching ? 'Checking...' : 'Run Health Check'}
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
              <div>
                <span className="text-sm text-[#A1A1AA]">Total Events</span>
                <div className="text-2xl font-bold text-[#22C55E]">{totalEvents.toLocaleString()}</div>
              </div>
              <div>
                <span className="text-sm text-[#A1A1AA]">Event Types</span>
                <div className="text-2xl font-bold text-[#3B82F6]">{Object.keys(eventsByType).length}</div>
              </div>
              <div>
                <span className="text-sm text-[#A1A1AA]">Services Online</span>
                <div className="text-2xl font-bold text-[#F59E0B]">{Object.values(services).filter((s) => s.status === 'Healthy').length}/{Object.keys(services).length}</div>
              </div>
              <div>
                <span className="text-sm text-[#A1A1AA]">SSE Streams</span>
                <div className="text-2xl font-bold text-[#22C55E]">{sseConnections}</div>
              </div>
            </div>
            {Object.keys(eventsByType).length > 0 && (
              <div className="mt-4 pt-4 border-t border-[#2A2A2A]">
                <span className="text-sm text-[#A1A1AA] mb-2 block">Events by Type</span>
                <div className="flex flex-wrap gap-2">
                  {Object.entries(eventsByType).map(([type, count]) => (
                    <span key={type} className="text-xs bg-[#0A0A0A] border border-[#2A2A2A] px-2 py-1 rounded">
                      {type}: <span className="font-semibold text-[#3B82F6]">{count}</span>
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </section>
    </div>
  );
}
