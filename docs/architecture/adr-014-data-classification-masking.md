# ADR-014: Data Classification & Field Masking

**Status:** Accepted  
**Date:** 2026-04-28  
**Regulation:** POJK No. 6/POJK.07/2022 (Perlindungan Konsumen), UU PDP (Personal Data Protection)

## Context

Indonesia's Personal Data Protection Law (UU PDP) and OJK POJK No. 6 require that personal data be classified, minimised in exposure, and masked in non-production contexts and API responses where full values are not required.

## Decision

### Classification Levels (`DataClassificationLevel` enum)

```
Public < Internal < Confidential < Restricted < PII < SensitivePII
```

### Masking Rules (`DataMaskingService`)

| Rule | Behaviour | Example |
|------|-----------|---------|
| Full | All characters replaced with * | `SecretValue` → `***********` |
| Partial | Expose first ~25% and last ~25% | `1234567890AB` → `12*****0AB` |
| LastFour | Show last 4 digits only | `1234567890` → `******7890` |
| Email | Show first 2 chars + domain | `john@example.com` → `jo***@example.com` |
| Phone | Show last 4 digits | `081234567890` → `*******7890` |

### Seeded Data Classifications

| Entity | Field | Level | Masking |
|--------|-------|-------|---------|
| User | IdNumber | SensitivePII | Partial |
| User | FullName | PII | Partial |
| User | Email | PII | Email |
| User | PhoneNumber | PII | Phone |
| Transaction | Amount | Confidential | Full |

## Consequences

- `DataClassification` table is seeded at migration time — no manual setup required.
- `GET /api/data-masking/classifications` returns all registered field classifications.
- `POST /api/data-masking/mask` is a stateless masking endpoint usable by any service.
- Retention years are tracked per field classification for data deletion scheduling.
