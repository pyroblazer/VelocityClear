import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import HsmKeyManagement from './HsmKeyManagement';

describe('HsmKeyManagement', () => {
  it('renders the section heading', () => {
    render(<HsmKeyManagement />);
    expect(screen.getByText('HSM Key Management')).toBeInTheDocument();
  });

  it('shows generate key button', () => {
    render(<HsmKeyManagement />);
    expect(screen.getByTestId('generate-key-btn')).toBeInTheDocument();
  });

  it('shows key type select dropdown', () => {
    render(<HsmKeyManagement />);
    expect(screen.getByTestId('key-type-select')).toBeInTheDocument();
  });

  it('shows key ID input', () => {
    render(<HsmKeyManagement />);
    expect(screen.getByTestId('key-id-input')).toBeInTheDocument();
  });

  it('generate button is disabled when key ID is empty', () => {
    render(<HsmKeyManagement />);
    expect(screen.getByTestId('generate-key-btn')).toBeDisabled();
  });

  it('shows key type options in select', () => {
    render(<HsmKeyManagement />);
    const select = screen.getByTestId('key-type-select') as HTMLSelectElement;
    expect(select.options.length).toBe(5);
  });
});
