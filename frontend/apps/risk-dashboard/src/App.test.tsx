import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import App from './App';

// Recharts uses ResizeObserver which is not in jsdom
global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
};

describe('Risk Dashboard App', () => {
  it('renders the Risk Monitoring Dashboard header', () => {
    render(<App />);
    expect(screen.getByText('Risk Monitoring Dashboard')).toBeInTheDocument();
  });

  it('shows the current risk score (68)', () => {
    render(<App />);
    expect(screen.getByText('68')).toBeInTheDocument();
  });

  it('shows high risk alert banner when HIGH risk events exist', () => {
    render(<App />);
    expect(screen.getByText(/HIGH risk transaction/i)).toBeInTheDocument();
  });

  it('shows the correct high risk count (3 HIGH events in data)', () => {
    render(<App />);
    expect(screen.getByText(/3 HIGH risk transaction/i)).toBeInTheDocument();
  });

  it('shows all risk level badges in events table', () => {
    render(<App />);
    const highBadges = screen.getAllByText('HIGH');
    const mediumBadges = screen.getAllByText('MEDIUM');
    const lowBadges = screen.getAllByText('LOW');
    expect(highBadges.length).toBeGreaterThanOrEqual(1);
    expect(mediumBadges.length).toBeGreaterThanOrEqual(1);
    expect(lowBadges.length).toBeGreaterThanOrEqual(1);
  });

  it('renders transaction IDs in the events list', () => {
    render(<App />);
    expect(screen.getByText('TXN-2024-0847')).toBeInTheDocument();
    expect(screen.getByText('TXN-2024-0843')).toBeInTheDocument();
  });

  it('shows risk score values for each event', () => {
    render(<App />);
    expect(screen.getByText('92')).toBeInTheDocument();
    expect(screen.getByText('23')).toBeInTheDocument();
  });

  it('shows risk flag tags', () => {
    render(<App />);
    expect(screen.getAllByText('velocity').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('geo-anomaly').length).toBeGreaterThanOrEqual(1);
  });

  it('renders gauge scale labels (0, 50, 100)', () => {
    render(<App />);
    expect(screen.getByText('0')).toBeInTheDocument();
    expect(screen.getByText('50')).toBeInTheDocument();
    expect(screen.getByText('100')).toBeInTheDocument();
  });

  it('renders Live indicator', () => {
    render(<App />);
    expect(screen.getByText('Live')).toBeInTheDocument();
  });

  it('renders risk distribution chart section', () => {
    render(<App />);
    expect(screen.getByText(/risk distribution/i)).toBeInTheDocument();
  });

  it('renders recent risk events section', () => {
    render(<App />);
    expect(screen.getByText(/recent risk events/i)).toBeInTheDocument();
  });

  it('renders all 7 events from the dataset', () => {
    render(<App />);
    // All 7 transaction IDs should be present
    for (let i = 1; i <= 7; i++) {
      expect(screen.getByText(`TXN-2024-084${8 - i}`)).toBeInTheDocument();
    }
  });
});
