type LogLevel = 'error' | 'warn' | 'info';

interface LogEntry {
  ts: string;
  level: LogLevel;
  msg: string;
  detail?: string | undefined;
  url?: string | undefined;
}

const LOG_KEY = 'vc_logs';
const MAX_AGE_MS = 24 * 60 * 60 * 1000; // 24 hours

function read(): LogEntry[] {
  try {
    const raw = localStorage.getItem(LOG_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function write(entries: LogEntry[]) {
  try {
    localStorage.setItem(LOG_KEY, JSON.stringify(entries));
  } catch {
    // localStorage full — clear and retry
    localStorage.removeItem(LOG_KEY);
  }
}

function prune() {
  const cutoff = Date.now() - MAX_AGE_MS;
  const entries = read().filter((e) => new Date(e.ts).getTime() > cutoff);
  write(entries);
}

function log(level: LogLevel, msg: string, detail?: string) {
  prune();
  const entry: LogEntry = {
    ts: new Date().toISOString(),
    level,
    msg,
    detail,
    url: typeof window !== 'undefined' ? window.location.href : undefined,
  };
  const entries = read();
  entries.push(entry);
  // Cap at 500 entries to prevent unbounded growth
  write(entries.slice(-500));

  if (level === 'error') {
    console.error(`[VC] ${msg}`, detail ?? '');
  } else if (level === 'warn') {
    console.warn(`[VC] ${msg}`, detail ?? '');
  }
}

export const logger = {
  error: (msg: string, detail?: string) => log('error', msg, detail),
  warn: (msg: string, detail?: string) => log('warn', msg, detail),
  info: (msg: string, detail?: string) => log('info', msg, detail),
  getEntries: (): LogEntry[] => { prune(); return read(); },
  clear: () => localStorage.removeItem(LOG_KEY),
};
