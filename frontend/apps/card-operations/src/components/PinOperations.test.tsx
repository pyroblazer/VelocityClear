import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import PinOperations from './PinOperations';

describe('PinOperations', () => {
  it('renders the section heading', () => {
    render(<PinOperations />);
    expect(screen.getByText('PIN Operations')).toBeInTheDocument();
  });

  it('shows Encrypt PIN section', () => {
    render(<PinOperations />);
    expect(screen.getAllByText('Encrypt PIN').length).toBeGreaterThan(0);
  });

  it('shows Decrypt PIN Block section', () => {
    render(<PinOperations />);
    expect(screen.getAllByText('Decrypt PIN Block').length).toBeGreaterThan(0);
  });

  it('shows Verify PIN section', () => {
    render(<PinOperations />);
    expect(screen.getAllByText('Verify PIN').length).toBeGreaterThan(0);
  });

  it('encrypt button is disabled when fields are empty', () => {
    render(<PinOperations />);
    expect(screen.getByTestId('encrypt-btn')).toBeDisabled();
  });

  it('decrypt button is disabled when fields are empty', () => {
    render(<PinOperations />);
    expect(screen.getByTestId('decrypt-btn')).toBeDisabled();
  });

  it('verify button is disabled when fields are empty', () => {
    render(<PinOperations />);
    expect(screen.getByTestId('verify-btn')).toBeDisabled();
  });

  it('shows encrypt PIN input', () => {
    render(<PinOperations />);
    expect(screen.getByTestId('encrypt-pin-input')).toBeInTheDocument();
  });

  it('shows PAN input for encrypt', () => {
    render(<PinOperations />);
    expect(screen.getByTestId('encrypt-pan-input')).toBeInTheDocument();
  });
});
