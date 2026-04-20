# ADR-009: ISO 9564 Format 0 PIN Block with XOR-Then-3DES Encryption

**Status:** Accepted
**Date:** 2026-04-22
**ISO 27001 Controls:** A.10.1.1 (Policy on the use of cryptographic controls), A.10.1.2 (Key management)
**ISO 9001 Clauses:** 7.5

## Context

Card-present and card-not-present transactions require the cardholder's PIN to be transmitted from the point of entry to the issuing bank for verification. The PIN must be encrypted during transit to prevent interception by attackers. The encryption must be standardized to ensure interoperability with real Hardware Security Modules (HSMs) and payment networks (Visa, Mastercard, etc.).

The platform's PinEncryptionService simulates an HSM for development and testing, but the PIN block format and encryption algorithm must match production standards so that the simulation can be replaced with a real HSM without changing the transaction processing logic.

## Decision

We implemented ISO 9564 Format 0 (ANSI X9.8) PIN block construction and encryption with the following design:

1. **PIN Block Construction:** The clear PIN block is a 16-character hexadecimal string where the first nibble is the format identifier (0), the second nibble is the PIN length, the following nibbles contain the PIN digits padded with Fs, and the remaining nibbles are all Fs. For example, a 4-digit PIN "1234" produces the block `041234FFFFFFFFFF`.

2. **PAN Block Construction:** The PAN (Primary Account Number) block takes the last 12 digits of the PAN (excluding the check digit) and left-pads with zeros to 16 hex characters. For example, PAN `4111111111111111` produces `0011111111111111`.

3. **XOR Operation:** The clear PIN block is XORed with the PAN block to produce the formatted PIN block. This ties the encrypted PIN to a specific card number, preventing an attacker from substituting a different PAN to decrypt the PIN.

4. **Encryption:** The XOR result is encrypted using 3DES (Triple DES with double-length 16-byte keys, also known as 2-key 3DES) under a Zone PIN Key (ZPK). The simulated HSM stores AES-256-encrypted ZPKs, but the PIN block itself uses 3DES as required by the payment industry standard.

5. **PIN Verification:** The simulated HSM decrypts the PIN block, XORs with the stored PAN block, extracts the clear PIN, and compares it against the expected PIN for the card.

## Consequences

**Benefits:**
- ISO 9564 Format 0 is the most widely deployed PIN block format globally. It is supported by all major HSM vendors (Thales Luna, Atalla, Futurex) and all major payment networks (Visa, Mastercard, American Express). This ensures interoperability when the simulated HSM is replaced with a real one.
- The XOR with the PAN block is a critical security feature: even if an attacker intercepts the encrypted PIN block, they cannot decrypt it for a different card because the XOR output depends on the specific PAN.
- 3DES (double-length key) remains the mandated standard for PIN encryption per PCI HSM requirements. While 3DES is being deprecated for general encryption, the payment industry specifically requires it for PIN block encryption.
- The simulated HSM using AES-256 key storage provides a realistic development environment where PINs are encrypted and decrypted using the same algorithms as production, just without dedicated cryptographic hardware.

**Trade-offs:**
- 3DES has a block size of 64 bits, which limits the PIN block to 8 bytes (16 hex characters). This is sufficient for PINs (4-12 digits) but does not accommodate additional data in the PIN block.
- AES for PIN encryption is not yet widely adopted in the payment industry. While AES is more secure in general, switching to AES for PIN blocks would break interoperability with existing HSMs and payment networks. This decision will be revisited as the industry migrates to AES-based PIN encryption.
- The simulated HSM stores ZPKs encrypted with a master key. In production, the master key must be stored in tamper-resistant hardware (a real HSM). The simulation is for development only and must never be used in production.
- Format 0 does not support PIN randomness (an optional feature in ISO 9564-1 that adds random digits to the PIN block). Format 3 supports this but is less widely deployed.

**Risks:**
- The simulated HSM must be clearly documented as non-production. Deploying it with real card data would violate PCI-DSS requirements for HSM usage. CI/CD pipelines should ensure the simulated HSM is not included in production builds.
- Key management in the simulation (AES-256 encrypted ZPK store) does not meet PCI-DSS key management requirements (key ceremony, dual control, split knowledge). Production deployments must integrate with a PCI-certified HSM.
