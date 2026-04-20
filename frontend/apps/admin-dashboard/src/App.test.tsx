import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import App from './App';

describe('Admin Dashboard App', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders the Admin Control Panel header', () => {
    render(<App />);
    expect(screen.getByText('Admin Control Panel')).toBeInTheDocument();
  });

  it('renders all 5 service names', () => {
    render(<App />);
    expect(screen.getByText('API Gateway')).toBeInTheDocument();
    expect(screen.getByText('Transaction')).toBeInTheDocument();
    expect(screen.getByText('Risk')).toBeInTheDocument();
    expect(screen.getByText('Payment')).toBeInTheDocument();
    expect(screen.getByText('Compliance')).toBeInTheDocument();
  });

  it('shows healthy status labels for services', () => {
    render(<App />);
    const healthyBadges = screen.getAllByText('Healthy');
    expect(healthyBadges.length).toBeGreaterThanOrEqual(4);
  });

  it('shows degraded status label for Payment service', () => {
    render(<App />);
    expect(screen.getByText('Degraded')).toBeInTheDocument();
  });

  it('shows SSE active connections count', () => {
    render(<App />);
    expect(screen.getByText('24')).toBeInTheDocument();
  });

  it('shows InMemory as the initial event bus backend', () => {
    render(<App />);
    expect(screen.getAllByText('InMemory').length).toBeGreaterThanOrEqual(1);
  });

  it('renders Run Health Check button', () => {
    render(<App />);
    expect(screen.getByText('Run Health Check')).toBeInTheDocument();
  });

  it('health check shows Running state when clicked', () => {
    vi.useFakeTimers();
    render(<App />);
    const btn = screen.getByText('Run Health Check');
    fireEvent.click(btn);
    expect(screen.getByText('Running...')).toBeInTheDocument();
  });

  it('health check reverts after 2 seconds', async () => {
    vi.useFakeTimers();
    render(<App />);
    fireEvent.click(screen.getByText('Run Health Check'));
    await act(async () => { vi.advanceTimersByTime(2000); });
    expect(screen.getByText('Run Health Check')).toBeInTheDocument();
  });

  it('clear cache button shows Cache Cleared when clicked', () => {
    vi.useFakeTimers();
    render(<App />);
    const btn = screen.getByText('Clear Cache');
    fireEvent.click(btn);
    expect(screen.getByText('Cache Cleared!')).toBeInTheDocument();
  });

  it('clear cache reverts after 1.5 seconds', async () => {
    vi.useFakeTimers();
    render(<App />);
    fireEvent.click(screen.getByText('Clear Cache'));
    await act(async () => { vi.advanceTimersByTime(1500); });
    expect(screen.getByText('Clear Cache')).toBeInTheDocument();
  });

  it('View Metrics button toggles metrics panel', () => {
    render(<App />);
    expect(screen.queryByText('CPU Usage')).not.toBeInTheDocument();
    fireEvent.click(screen.getByText('View Metrics'));
    expect(screen.getByText('CPU Usage')).toBeInTheDocument();
  });

  it('View Metrics button hides metrics panel on second click', () => {
    render(<App />);
    fireEvent.click(screen.getByText('View Metrics'));
    fireEvent.click(screen.getByText('View Metrics'));
    expect(screen.queryByText('CPU Usage')).not.toBeInTheDocument();
  });

  it('metrics panel shows system metrics', () => {
    render(<App />);
    fireEvent.click(screen.getByText('View Metrics'));
    expect(screen.getByText('CPU Usage')).toBeInTheDocument();
    expect(screen.getByText('Memory')).toBeInTheDocument();
    expect(screen.getByText('Avg Latency')).toBeInTheDocument();
  });

  it('shows service port numbers', () => {
    render(<App />);
    expect(screen.getByText(':5000')).toBeInTheDocument();
    expect(screen.getByText(':5001')).toBeInTheDocument();
  });

  it('shows uptime percentages', () => {
    render(<App />);
    const uptime = screen.getAllByText(/\d+\.\d+%/);
    expect(uptime.length).toBeGreaterThanOrEqual(5);
  });
});
