import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import Iso8583Tools from './Iso8583Tools';

describe('Iso8583Tools', () => {
  it('renders the section heading', () => {
    render(<Iso8583Tools />);
    expect(screen.getByText('ISO 8583 Tools')).toBeInTheDocument();
  });

  it('shows Parse Message section', () => {
    render(<Iso8583Tools />);
    expect(screen.getByText('Parse Message')).toBeInTheDocument();
  });

  it('shows Build Message section', () => {
    render(<Iso8583Tools />);
    expect(screen.getByText('Build Message')).toBeInTheDocument();
  });

  it('shows Card Authorization section', () => {
    render(<Iso8583Tools />);
    expect(screen.getByText('Card Authorization')).toBeInTheDocument();
  });

  it('shows parse input textarea', () => {
    render(<Iso8583Tools />);
    expect(screen.getByTestId('parse-input')).toBeInTheDocument();
  });

  it('shows MTI input for build', () => {
    render(<Iso8583Tools />);
    expect(screen.getByTestId('build-mti-input')).toBeInTheDocument();
  });

  it('shows PAN input for authorization', () => {
    render(<Iso8583Tools />);
    expect(screen.getByTestId('auth-pan-input')).toBeInTheDocument();
  });

  it('shows amount input for authorization', () => {
    render(<Iso8583Tools />);
    expect(screen.getByTestId('auth-amount-input')).toBeInTheDocument();
  });

  it('authorize button is enabled when PAN has default value', () => {
    render(<Iso8583Tools />);
    expect(screen.getByTestId('authorize-btn')).toBeEnabled();
  });

  it('shows parse and build buttons', () => {
    render(<Iso8583Tools />);
    expect(screen.getByTestId('parse-btn')).toBeInTheDocument();
    expect(screen.getByTestId('build-btn')).toBeInTheDocument();
  });
});
