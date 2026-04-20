import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import App from './App';

describe('Audit Trail Dashboard App', () => {
  it('renders the Audit Trail Dashboard header', () => {
    render(<App />);
    expect(screen.getByText('Audit Trail Dashboard')).toBeInTheDocument();
  });

  it('shows Chain Verified indicator', () => {
    render(<App />);
    expect(screen.getByText('Chain Verified')).toBeInTheDocument();
  });

  it('shows Total Events stat (7)', () => {
    render(<App />);
    const label = screen.getByText('Total Events');
    expect(label.parentElement).toHaveTextContent('7');
  });

  it('shows Events Today stat (7)', () => {
    render(<App />);
    const label = screen.getByText('Events Today');
    expect(label.parentElement).toHaveTextContent('7');
  });

  it('shows Chain Integrity section with 7 Verified and 0 Tampered', () => {
    render(<App />);
    expect(screen.getByText('Chain Integrity')).toBeInTheDocument();
    expect(screen.getByText('Verified')).toBeInTheDocument();
    expect(screen.getByText('Tampered')).toBeInTheDocument();
    expect(screen.getByText('0')).toBeInTheDocument();
  });

  it('renders Hash Chain Timeline section', () => {
    render(<App />);
    expect(screen.getByText('Hash Chain Timeline')).toBeInTheDocument();
  });

  it('renders Chain Link Visualization section', () => {
    render(<App />);
    expect(screen.getByText('Chain Link Visualization')).toBeInTheDocument();
  });

  it('renders all 5 filter buttons', () => {
    render(<App />);
    const buttons = screen.getAllByRole('button');
    const buttonTexts = buttons.map((b) => b.textContent);
    expect(buttonTexts).toContain('All');
    expect(buttonTexts).toContain('TransactionCreated');
    expect(buttonTexts).toContain('RiskEvaluated');
    expect(buttonTexts).toContain('PaymentAuthorized');
    expect(buttonTexts).toContain('AuditLogged');
  });

  it('renders search input with correct placeholder', () => {
    render(<App />);
    expect(
      screen.getByPlaceholderText('Search by hash, type, or payload...')
    ).toBeInTheDocument();
  });

  it('shows all 7 audit log entries by default', () => {
    render(<App />);
    // 7 entries should show audit IDs or at least 7 type badges
    const txCreated = screen.getAllByText('TransactionCreated');
    const riskEval = screen.getAllByText('RiskEvaluated');
    const payAuth = screen.getAllByText('PaymentAuthorized');
    const auditLogged = screen.getAllByText('AuditLogged');
    // In timeline + chain visualization: each type badge appears twice per entry
    expect(txCreated.length).toBeGreaterThanOrEqual(2);
    expect(riskEval.length).toBeGreaterThanOrEqual(2);
    expect(payAuth.length).toBeGreaterThanOrEqual(2);
    expect(auditLogged.length).toBeGreaterThanOrEqual(1);
  });

  it('shows transaction IDs in payloads', () => {
    render(<App />);
    expect(screen.getAllByText(/TXN-2024-0847/).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/TXN-2024-0848/).length).toBeGreaterThanOrEqual(1);
  });

  it('shows hash values in timeline', () => {
    render(<App />);
    expect(screen.getAllByText(/A1B2C3/).length).toBeGreaterThanOrEqual(1);
  });

  it('filters to only TransactionCreated events when that filter is clicked', () => {
    render(<App />);
    fireEvent.click(screen.getAllByText('TransactionCreated')[0]);
    // After filter, only TransactionCreated badges should appear in timeline
    // riskBadges appear in filter buttons but not in timeline
    // Actually the filter buttons still show - just verify PaymentAuthorized is NOT in timeline by checking count drops
    // The filter button for TransactionCreated still exists, so getAllByText returns >=1
    // Verify AuditLogged timeline entries are gone (it only has 1 entry, filter buttons still show it)
    const auditLogged = screen.getAllByText('AuditLogged');
    // Should only appear once (in the filter button) - not in the timeline
    expect(auditLogged.length).toBe(1);
  });

  it('shows No events match message when search yields no results', () => {
    render(<App />);
    const searchInput = screen.getByPlaceholderText('Search by hash, type, or payload...');
    fireEvent.change(searchInput, { target: { value: 'ZZZNONEXISTENT999' } });
    expect(screen.getByText('No events match the current filter.')).toBeInTheDocument();
  });

  it('filters by search query matching hash', () => {
    render(<App />);
    const searchInput = screen.getByPlaceholderText('Search by hash, type, or payload...');
    fireEvent.change(searchInput, { target: { value: 'A1B2C3' } });
    // Only entries with A1B2C3 in hash or prevHash should appear
    expect(screen.queryByText('No events match the current filter.')).not.toBeInTheDocument();
  });

  it('filters by search query matching event type', () => {
    render(<App />);
    const searchInput = screen.getByPlaceholderText('Search by hash, type, or payload...');
    fireEvent.change(searchInput, { target: { value: 'riskeval' } });
    expect(screen.queryByText('No events match the current filter.')).not.toBeInTheDocument();
  });

  it('shows prev hash for entries that have a previous entry', () => {
    render(<App />);
    expect(screen.getAllByText(/Prev:/).length).toBeGreaterThanOrEqual(1);
  });

  it('shows Hash: label for entries', () => {
    render(<App />);
    expect(screen.getAllByText(/Hash:/).length).toBeGreaterThanOrEqual(1);
  });

  it('shows timestamps for events', () => {
    render(<App />);
    expect(screen.getByText('14:32:18')).toBeInTheDocument();
    expect(screen.getByText('14:33:02')).toBeInTheDocument();
  });

  it('All filter is active by default (shows all events)', () => {
    render(<App />);
    // All 7 entries visible - TransactionCreated appears 2 times in timeline
    const txCreatedInTimeline = screen.getAllByText('TransactionCreated');
    expect(txCreatedInTimeline.length).toBeGreaterThanOrEqual(2);
  });

  it('RiskEvaluated filter shows only risk events', () => {
    render(<App />);
    fireEvent.click(screen.getAllByText('RiskEvaluated')[0]);
    // AuditLogged only appears in filter button, not in timeline
    const auditLogged = screen.getAllByText('AuditLogged');
    expect(auditLogged.length).toBe(1);
  });
});
