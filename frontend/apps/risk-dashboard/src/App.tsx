import { useMemo } from 'react';
import { AlertTriangle, Shield, TrendingUp, Activity } from 'lucide-react';
import {
  PieChart,
  Pie,
  Cell,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from 'recharts';

const RISK_COLORS = {
  HIGH: '#EF4444',
  MEDIUM: '#F59E0B',
  LOW: '#22C55E',
} as const;

const riskDistribution = [
  { name: 'HIGH', value: 12, color: RISK_COLORS.HIGH },
  { name: 'MEDIUM', value: 35, color: RISK_COLORS.MEDIUM },
  { name: 'LOW', value: 53, color: RISK_COLORS.LOW },
];

interface RiskTrendPoint {
  time: string;
  score: number;
}

const riskTrendData: RiskTrendPoint[] = [
  { time: '00:00', score: 32 },
  { time: '02:00', score: 28 },
  { time: '04:00', score: 45 },
  { time: '06:00', score: 52 },
  { time: '08:00', score: 67 },
  { time: '10:00', score: 72 },
  { time: '12:00', score: 58 },
  { time: '14:00', score: 81 },
  { time: '16:00', score: 74 },
  { time: '18:00', score: 63 },
  { time: '20:00', score: 55 },
  { time: '22:00', score: 48 },
];

type RiskLevel = 'HIGH' | 'MEDIUM' | 'LOW';

interface RiskEvent {
  id: string;
  transactionId: string;
  score: number;
  level: RiskLevel;
  flags: string[];
  time: string;
}

const recentEvents: RiskEvent[] = [
  { id: '1', transactionId: 'TXN-2024-0847', score: 92, level: 'HIGH', flags: ['velocity', 'geo-anomaly'], time: '14:32:18' },
  { id: '2', transactionId: 'TXN-2024-0846', score: 78, level: 'MEDIUM', flags: ['amount-threshold'], time: '14:31:45' },
  { id: '3', transactionId: 'TXN-2024-0845', score: 23, level: 'LOW', flags: [], time: '14:30:12' },
  { id: '4', transactionId: 'TXN-2024-0844', score: 88, level: 'HIGH', flags: ['pattern-match', 'new-device'], time: '14:29:55' },
  { id: '5', transactionId: 'TXN-2024-0843', score: 45, level: 'MEDIUM', flags: ['cross-border'], time: '14:28:30' },
  { id: '6', transactionId: 'TXN-2024-0842', score: 15, level: 'LOW', flags: [], time: '14:27:11' },
  { id: '7', transactionId: 'TXN-2024-0841', score: 81, level: 'HIGH', flags: ['velocity', 'amount-threshold'], time: '14:26:03' },
];

const currentScore = 68;

function getScoreColor(score: number): string {
  if (score >= 80) return RISK_COLORS.HIGH;
  if (score >= 50) return RISK_COLORS.MEDIUM;
  return RISK_COLORS.LOW;
}

function getLevelBadge(level: RiskLevel) {
  const colors: Record<RiskLevel, string> = {
    HIGH: 'bg-[#EF4444]/20 text-[#EF4444] border-[#EF4444]/30',
    MEDIUM: 'bg-[#F59E0B]/20 text-[#F59E0B] border-[#F59E0B]/30',
    LOW: 'bg-[#22C55E]/20 text-[#22C55E] border-[#22C55E]/30',
  };
  return (
    <span className={`text-xs font-semibold px-2 py-0.5 rounded border ${colors[level]}`}>
      {level}
    </span>
  );
}

function RiskGauge({ score }: { score: number }) {
  const color = getScoreColor(score);
  const angle = (score / 100) * 180;

  return (
    <div className="flex flex-col items-center">
      <div className="relative w-56 h-28 overflow-hidden">
        {/* Background arc */}
        <div
          className="absolute w-56 h-56 rounded-full border-[12px] border-[#2A2A2A]"
          style={{ clipPath: 'polygon(0 0, 100% 0, 100% 50%, 0 50%)' }}
        />
        {/* Colored arc */}
        <svg className="absolute inset-0 w-56 h-28" viewBox="0 0 224 112">
          <path
            d="M 12 100 A 100 100 0 0 1 212 100"
            fill="none"
            stroke={color}
            strokeWidth="12"
            strokeDasharray={`${(angle / 180) * 314.16} 314.16`}
            strokeLinecap="round"
          />
        </svg>
        {/* Needle */}
        <svg className="absolute inset-0 w-56 h-28" viewBox="0 0 224 112">
          <line
            x1="112"
            y1="100"
            x2={112 + 90 * Math.cos(Math.PI - (angle * Math.PI) / 180)}
            y2={100 - 90 * Math.sin((angle * Math.PI) / 180)}
            stroke={color}
            strokeWidth="2"
            strokeLinecap="round"
          />
          <circle cx="112" cy="100" r="6" fill={color} />
        </svg>
        {/* Score text */}
        <div className="absolute bottom-0 left-1/2 -translate-x-1/2 text-center">
          <span className="text-3xl font-bold" style={{ color }}>
            {score}
          </span>
        </div>
      </div>
      <div className="flex justify-between w-56 mt-1 text-xs text-[#A1A1AA]">
        <span>0</span>
        <span>50</span>
        <span>100</span>
      </div>
    </div>
  );
}

function App() {
  const highRiskCount = useMemo(() => recentEvents.filter((e) => e.level === 'HIGH').length, []);

  return (
    <div className="min-h-screen bg-[#0A0A0A] text-white p-6">
      {/* Header */}
      <header className="flex items-center gap-3 mb-8 border-b border-[#2A2A2A] pb-4">
        <Shield className="w-8 h-8 text-[#3B82F6]" />
        <h1 className="text-3xl font-bold">Risk Monitoring Dashboard</h1>
        <div className="ml-auto flex items-center gap-2">
          <Activity className="w-4 h-4 text-[#22C55E]" />
          <span className="text-sm text-[#A1A1AA]">Live</span>
        </div>
      </header>

      {/* Alert Banner */}
      {highRiskCount > 0 && (
        <div className="mb-6 bg-[#EF4444]/10 border border-[#EF4444]/30 rounded-lg p-4 flex items-center gap-3">
          <AlertTriangle className="w-5 h-5 text-[#EF4444] shrink-0" />
          <span className="text-[#EF4444] font-medium">
            {highRiskCount} HIGH risk transaction{highRiskCount > 1 ? 's' : ''} detected in recent events. Immediate review recommended.
          </span>
        </div>
      )}

      {/* Top Row: Gauge + Pie Chart */}
      <section className="mb-8 grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Risk Score Gauge */}
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <TrendingUp className="w-5 h-5 text-[#3B82F6]" />
            Current Risk Score
          </h2>
          <RiskGauge score={currentScore} />
        </div>

        {/* Risk Distribution Pie Chart */}
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
          <h2 className="text-lg font-semibold mb-4">Risk Distribution</h2>
          <ResponsiveContainer width="100%" height={220}>
            <PieChart>
              <Pie
                data={riskDistribution}
                cx="50%"
                cy="50%"
                innerRadius={50}
                outerRadius={80}
                paddingAngle={4}
                dataKey="value"
                label={({ name, value }) => `${name}: ${value}`}
              >
                {riskDistribution.map((entry) => (
                  <Cell key={entry.name} fill={entry.color} />
                ))}
              </Pie>
              <Tooltip
                contentStyle={{ backgroundColor: '#1A1A1A', border: '1px solid #2A2A2A', borderRadius: '8px' }}
                itemStyle={{ color: '#FFFFFF' }}
              />
              <Legend />
            </PieChart>
          </ResponsiveContainer>
        </div>
      </section>

      {/* Risk Trend Line Chart */}
      <section className="mb-8">
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
          <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
            <Activity className="w-5 h-5 text-[#3B82F6]" />
            Risk Trend (24h)
          </h2>
          <ResponsiveContainer width="100%" height={280}>
            <LineChart data={riskTrendData}>
              <CartesianGrid strokeDasharray="3 3" stroke="#2A2A2A" />
              <XAxis dataKey="time" stroke="#A1A1AA" fontSize={12} />
              <YAxis domain={[0, 100]} stroke="#A1A1AA" fontSize={12} />
              <Tooltip
                contentStyle={{ backgroundColor: '#1A1A1A', border: '1px solid #2A2A2A', borderRadius: '8px' }}
                itemStyle={{ color: '#FFFFFF' }}
                labelStyle={{ color: '#A1A1AA' }}
              />
              <Legend />
              <Line
                type="monotone"
                dataKey="score"
                stroke="#3B82F6"
                strokeWidth={2}
                dot={{ fill: '#3B82F6', r: 3 }}
                activeDot={{ r: 5 }}
                name="Risk Score"
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </section>

      {/* Recent Risk Events Table */}
      <section>
        <h2 className="text-xl font-semibold mb-4 flex items-center gap-2">
          <AlertTriangle className="w-5 h-5 text-[#F59E0B]" />
          Recent Risk Events
        </h2>
        <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[#2A2A2A] text-[#A1A1AA]">
                  <th className="text-left p-3 font-medium">Transaction ID</th>
                  <th className="text-left p-3 font-medium">Score</th>
                  <th className="text-left p-3 font-medium">Level</th>
                  <th className="text-left p-3 font-medium">Flags</th>
                  <th className="text-left p-3 font-medium">Time</th>
                </tr>
              </thead>
              <tbody>
                {recentEvents.map((event) => (
                  <tr
                    key={event.id}
                    className="border-b border-[#2A2A2A]/50 hover:bg-[#2A2A2A]/30 transition-colors"
                  >
                    <td className="p-3 font-mono text-[#3B82F6]">{event.transactionId}</td>
                    <td className="p-3">
                      <span className="font-semibold" style={{ color: getScoreColor(event.score) }}>
                        {event.score}
                      </span>
                    </td>
                    <td className="p-3">{getLevelBadge(event.level)}</td>
                    <td className="p-3">
                      {event.flags.length > 0 ? (
                        <div className="flex gap-1 flex-wrap">
                          {event.flags.map((flag) => (
                            <span
                              key={flag}
                              className="text-xs bg-[#0A0A0A] text-[#A1A1AA] px-2 py-0.5 rounded border border-[#2A2A2A]"
                            >
                              {flag}
                            </span>
                          ))}
                        </div>
                      ) : (
                        <span className="text-[#A1A1AA]">--</span>
                      )}
                    </td>
                    <td className="p-3 text-[#A1A1AA]">{event.time}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>
    </div>
  );
}

export default App;
