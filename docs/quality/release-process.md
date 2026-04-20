# Release Process

**Platform:** VelocityClear Adaptive Real-Time Financial Transaction Platform
**Last Reviewed:** 2026-04-22
**Owner:** Development & Operations Teams

---

## Overview

This document defines the release process for the VelocityClear platform. It covers the criteria for release readiness, the step-by-step release procedure, versioning conventions, and changelog requirements. The process ensures that every release meets quality, security, and functionality standards before reaching production.

---

## Release Readiness Criteria

A release is considered ready for deployment only when ALL of the following criteria are met:

### 1. Code Review

- All changes have been submitted via pull request to the `main` branch
- At least one reviewer has approved the pull request
- All review comments have been addressed
- No outstanding change requests or hold tags

### 2. CI Pipeline

All CI jobs must pass with green status:

| Job | Requirement |
|-----|-------------|
| `backend-lint` | `dotnet format --verify-no-changes` passes with zero issues |
| `backend-unit-tests` | All unit tests pass (`dotnet test tests/FinancialPlatform.UnitTests`) |
| `backend-integration-tests` | All integration tests pass (`dotnet test tests/FinancialPlatform.IntegrationTests`) |
| `frontend-build` (matrix) | All six frontend apps build and lint successfully (superapp, transaction-ui, admin-dashboard, risk-dashboard, audit-dashboard, card-operations) |
| `frontend-e2e` | All Playwright end-to-end tests pass |
| `newman-tests` | All Postman API tests pass against the running services |
| `trivy-scan` | Zero CRITICAL or HIGH severity findings |

### 3. Security Scan

- Trivy filesystem scan must report zero CRITICAL findings
- Trivy IaC configuration scan must report zero CRITICAL findings
- HIGH findings must be documented with a remediation plan if they cannot be fixed before release

### 4. API Contract Validation

- Newman API tests (25+ requests) must all pass
- Any new API endpoints must have corresponding Newman test cases
- Breaking API changes must be documented and communicated to consumers

### 5. Audit Chain Integrity

- Audit chain verification (`GET /api/audit/verify`) must pass on the release candidate
- No manual modifications to audit log data

---

## Versioning Convention

The platform follows **Semantic Versioning (SemVer 2.0.0)**:

```
MAJOR.MINOR.PATCH
```

### Version Components

| Component | Incremented When | Example |
|-----------|------------------|---------|
| **MAJOR** | Breaking API changes, incompatible database schema changes, major architecture changes | 1.0.0 to 2.0.0 |
| **MINOR** | New features, new API endpoints, non-breaking enhancements, database schema additions | 1.0.0 to 1.1.0 |
| **PATCH** | Bug fixes, security patches, dependency updates, documentation corrections | 1.0.0 to 1.0.1 |

### Pre-release Identifiers

Pre-release versions may be appended with a hyphen and identifier:

```
1.2.0-alpha.1
1.2.0-beta.3
1.2.0-rc.1
```

- **alpha:** Internal testing, feature incomplete, not for external review
- **beta:** Feature complete, external testing permitted, API may change
- **rc:** Release candidate, no further changes unless bugs found, expected to become the release

### Build Metadata

Build metadata may be appended with a plus sign:

```
1.2.0+sha.f6c9d35
1.2.0-rc.1+build.42
```

---

## Release Procedure

### Step 1: Create Release Branch

```bash
git checkout main
git pull origin main
git checkout -b release/vX.Y.Z
```

### Step 2: Update Version Numbers

Update version numbers in the following locations:

- `Directory.Build.props` (backend version)
- Each frontend app's `package.json` (frontend version)
- `infrastructure/docker-compose.yml` image tags (if applicable)
- Any user-facing version displays

### Step 3: Generate Changelog

Generate the changelog from git history since the last release tag:

```bash
git log vPREVIOUS..HEAD --oneline --no-merges
```

Categorize changes into:
- **Added:** New features
- **Changed:** Changes to existing functionality
- **Deprecated:** Features to be removed in future releases
- **Removed:** Features removed in this release
- **Fixed:** Bug fixes
- **Security:** Security vulnerability fixes

### Step 4: Final Validation

```bash
# Run the full CI pipeline locally
dotnet restore
dotnet build -c Release
dotnet test tests/FinancialPlatform.UnitTests -c Release
dotnet test tests/FinancialPlatform.IntegrationTests -c Release
dotnet format --verify-no-changes

# Run Trivy scan
trivy fs . --severity CRITICAL,HIGH
trivy config . --severity CRITICAL,HIGH

# Build all frontend apps
for app in superapp transaction-ui admin-dashboard risk-dashboard audit-dashboard card-operations; do
  cd frontend/apps/$app && npm ci && npm run build && npm run lint && npm test && cd ../../..
done

# Run Newman tests (requires backend services running)
cd postman && newman run FinancialPlatform.postman_collection.json -e FinancialPlatform-Local.postman_environment.json
```

### Step 5: Commit and Tag

```bash
git add .
git commit -m "release: vX.Y.Z"
git tag -a vX.Y.Z -m "Release vX.Y.Z

## Changes
- [Summary of key changes]
"
git push origin release/vX.Y.Z --tags
```

### Step 6: Build Docker Images

```bash
cd infrastructure
docker-compose build
```

Verify that all images build successfully:
- `velocityclear/apigateway:X.Y.Z`
- `velocityclear/transaction-service:X.Y.Z`
- `velocityclear/risk-service:X.Y.Z`
- `velocityclear/payment-service:X.Y.Z`
- `velocityclear/compliance-service:X.Y.Z`
- `velocityclear/pin-encryption-service:X.Y.Z`
- `velocityclear/transaction-ui:X.Y.Z`
- `velocityclear/admin-dashboard:X.Y.Z`
- `velocityclear/risk-dashboard:X.Y.Z`
- `velocityclear/audit-dashboard:X.Y.Z`
- `velocityclear/card-operations:X.Y.Z`
- `velocityclear/superapp:X.Y.Z`

### Step 7: Deploy to Staging

Deploy the release candidate to the staging environment:

```bash
docker-compose -f docker-compose.yml -f docker-compose.staging.yml up -d
```

Run smoke tests:
- Health check on all services
- Create a test transaction and verify end-to-end flow
- Verify audit chain integrity
- Check Prometheus targets are all up
- Verify Grafana dashboards display data

### Step 8: Merge to Main and Deploy to Production

```bash
git checkout main
git merge release/vX.Y.Z
git push origin main
```

Deploy to production:

```bash
docker-compose up -d
```

### Step 9: Post-Release Verification

Within 30 minutes of production deployment:

1. Verify all service health checks pass
2. Process a test transaction end-to-end
3. Verify audit chain integrity via `GET /api/audit/verify`
4. Check Prometheus alerts for any firing alerts
5. Monitor Grafana dashboards for error rate and latency spikes
6. Verify SSE stream delivers events to connected frontends

### Step 10: Clean Up

```bash
# Delete the release branch
git branch -d release/vX.Y.Z
git push origin --delete release/vX.Y.Z
```

---

## Hotfix Procedure

For urgent production fixes that cannot wait for the normal release cycle:

### Step 1: Create Hotfix Branch

```bash
git checkout main
git checkout -b hotfix/vX.Y.Z+1
```

### Step 2: Apply Fix

Apply the minimal fix required to resolve the production issue. The fix must be as small as possible to reduce risk.

### Step 3: Validate

- All CI pipeline jobs must pass
- Trivy scan must pass with zero CRITICAL/HIGH findings
- The specific issue being fixed must be verified as resolved

### Step 4: Release

Follow the standard release procedure (Steps 5-9) with the PATCH version incremented.

### Step 5: Document

- Update the changelog with the hotfix details
- Create a post-incident review if the hotfix addresses a production incident

---

## Rollback Procedure

If a release introduces a critical issue that cannot be hotfixed within the RTO (1 hour):

### Step 1: Assess

Determine whether a rollback is necessary based on:
- Severity of the issue (P1 or P2)
- Estimated time to hotfix vs. rollback time
- Data impact (is any data migration involved?)

### Step 2: Rollback

```bash
# Stop the current version
docker-compose down

# Check out the previous release tag
git checkout vPREVIOUS.X.Y.Z

# Rebuild and start
docker-compose build
docker-compose up -d
```

### Step 3: Verify

- All health checks pass
- Transactions process successfully
- Audit chain integrity verified

### Step 4: Communicate

- Notify stakeholders of the rollback
- Document the reason and timeline in the incident ticket
- Schedule a post-incident review

---

## Changelog Format

Each release has a changelog entry in the following format:

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added
- Description of new features

### Changed
- Description of changes to existing functionality

### Deprecated
- Description of features to be removed in future releases

### Removed
- Description of features removed in this release

### Fixed
- Description of bug fixes

### Security
- Description of security vulnerability fixes
```

The changelog is maintained in a `CHANGELOG.md` file at the repository root and is updated with every release.

---

## Release Calendar

| Frequency | Type | Typical Content |
|-----------|------|-----------------|
| As needed | PATCH | Bug fixes, security patches |
| Bi-weekly | MINOR | New features, enhancements |
| Quarterly | MAJOR | Breaking changes, major features (advance notice required) |
| As needed | HOTFIX | Urgent production fixes |

---

## Change Log

| Date | Version | Author | Description |
|------|---------|--------|-------------|
| 2026-04-22 | 1.0 | Development & Operations Teams | Initial release process documentation |
