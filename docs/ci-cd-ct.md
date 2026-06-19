# Oyako CI/CD/CT Guide

## 1. Purpose

This document describes comprehensive Continuous Integration, Continuous Delivery, and Continuous Testing practices for Oyako across GitHub Actions and Azure DevOps. It is a process guide and pipeline blueprint, not a committed workflow implementation.

## 2. Pipeline Goals

The pipeline must protect three outcomes:

- The backend builds, tests, and exposes reliable health/knowledge/chat APIs.
- The frontend builds, passes linting, passes Playwright e2e tests, and passes Axe accessibility checks.
- The full stack can start together, refresh knowledge when required, and answer questions from current knowledge sources.

## 3. Branch and Validation Strategy

For pre-alpha development, the main branch may receive direct feature cutovers, but every change should still be validated with automated checks.

Recommended gates:

- Pull request: restore, build, unit tests, frontend build, lint, mock e2e, a11y.
- Main branch: all PR checks plus full-stack smoke.
- Nightly: special knowledge-refresh tests, real crawler/scraper, provider connectivity, dependency audit.
- Release candidate: staging deploy, post-deploy smoke, rollback rehearsal.

## 4. Continuous Integration

### 4.1 Backend CI

Backend CI should run:

1. Checkout source.
2. Install .NET SDK 10.
3. Restore `webapi-oyako/webapi-oyako.csproj` and test project dependencies.
4. Build in Release configuration.
5. Run `dotnet test`.
6. Publish test results.
7. Publish backend artifact when running on main or release branches.

Optional checks:

- Treat warnings as errors after pre-alpha stabilizes.
- Collect code coverage.
- Run Playwright browser install if backend crawler tests require browser binaries.
- Validate `appsettings.json` schema expectations.

### 4.2 Frontend CI

Frontend CI should run:

1. Checkout source.
2. Install Node.js LTS.
3. Run `npm ci` inside `webapp-oyako`.
4. Run `npm run build`.
5. Run `npm run lint`.
6. Run `npm run test:e2e`.
7. Run `npm run test:a11y`.
8. Publish Playwright reports and screenshots on failure.
9. Publish Vite build artifact when running on main or release branches.

### 4.3 Full-Stack CI

Full-stack CI should run after backend and frontend are individually green.

1. Start backend on 5000/5001.
2. Start frontend on 3000.
3. Wait for `/api/api-health` and `/api/knowledge-health`.
4. Run mock-independent smoke tests.
5. Optionally call `/api/knowledge-refresh` in special test mode.
6. Ask a representative UI question.
7. Stop both processes and upload logs.

## 5. Continuous Testing

### 5.1 Unit Tests

Unit tests cover isolated logic such as URL normalization, text cleaning, markdown sanitization, AI response parsing, and runtime status state.

### 5.2 Integration Tests

Integration tests cover repositories, SQLite schema behavior, backup/restore, ready question replacement, and cache persistence.

### 5.3 Contract Tests

Contract tests should ensure frontend types match backend payload shapes, especially runtime status, knowledge sources, ready questions, settings, and chat stream events.

### 5.4 E2E Tests

E2E tests should validate user-visible flows:

- App loads and status bar is never blank.
- User asks a question and receives an answer.
- Latest suggested questions are clickable.
- Knowledge Bank opens and shows clean previews.
- Knowledge refresh progresses through all steps.
- Settings save shows accessible progress.
- Help page reflects current features.

### 5.5 Accessibility Tests

Axe checks should run for:

- Main app screen.
- Knowledge Bank dialog.
- Settings dialog.
- Help dialog.
- Utility and user menus.

Manual keyboard checks should confirm Tab order, Escape behavior, focus rings, and live region announcements.

### 5.6 Special Knowledge Refresh Tests

Knowledge refresh is slower and depends on external systems. It should be separated from fast PR checks.

Special tests should verify:

- `/api/knowledge-refresh` completes successfully.
- Backup/restore protects old data on failure.
- Clean previews do not begin with repeated navigation text.
- System instruction cache rebuilds after source change.
- Ready questions refresh after knowledge change.
- The UI uses refreshed data after completion.

## 6. Continuous Delivery

### 6.1 Artifact Strategy

Backend artifacts should include the published ASP.NET app, configuration templates, and deployment metadata. Frontend artifacts should include the Vite `dist` output.

SQLite should be treated carefully. For production, the database file should live in persistent storage, not inside ephemeral deployment artifacts.

### 6.2 Environment Strategy

Recommended environments:

- Local: developer machine, Ollama optional, Azure optional.
- Dev: automated integration environment.
- Test: stable QA environment with deterministic mock-friendly tests.
- Staging: production-like Azure AI and persistent SQLite storage.
- Production: locked secrets, monitored health endpoints, manual approval gates.

### 6.3 Secret Management

Secrets must not be committed.

- GitHub Actions should use GitHub repository/environment secrets.
- Azure DevOps should use Library variable groups or Azure Key Vault integration.
- Azure AI API keys should map to the same environment variable naming expected by backend options.
- `oyako.env` is for local development only.

### 6.4 Deployment Targets

Current minimal deployment targets:

- `deploy-aca.cmd`: one Azure Container Registry Basic SKU, one Azure Container Apps Environment, and one always-on Container App in `italynorth`, using the single image `oyako:latest`.
- `deploy-awa.cmd`: one Linux Basic B1 App Service Plan and one Azure Web App in `italynorth`, using direct ZIP deploy from locally published `linux-x64` source and startup-time Playwright dependency installation.

Both targets serve the React SPA and ASP.NET API from one public hostname, keep SQLite as an app-local file, and require secure environment variables for AI providers. They do not create Azure Storage, Static Web Apps, separate API apps, Key Vault, Application Insights, Redis, Cosmos DB, or a managed SQL/PostgreSQL/MySQL database.

## 7. GitHub Actions Blueprint

A GitHub Actions workflow should contain jobs similar to:

```yaml
name: oyako-ci
on:
  pull_request:
  push:
    branches: [main]
jobs:
  backend:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore webapi-oyako/webapi-oyako.csproj
      - run: dotnet build webapi-oyako/webapi-oyako.csproj -c Release --no-restore
      - run: dotnet test webapi-oyako/webapi-oyako.Tests/webapi-oyako.Tests.csproj -c Release --no-build
  frontend:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: npm
          cache-dependency-path: webapp-oyako/package-lock.json
      - run: npm ci
        working-directory: webapp-oyako
      - run: npm run build
        working-directory: webapp-oyako
      - run: npm run lint
        working-directory: webapp-oyako
      - run: npx playwright install --with-deps chromium
        working-directory: webapp-oyako
      - run: npm run test:e2e
        working-directory: webapp-oyako
      - run: npm run test:a11y
        working-directory: webapp-oyako
```

A release workflow can add artifact upload, environment approvals, deployment, and post-deploy smoke tests.

## 8. Azure DevOps Blueprint

An Azure DevOps pipeline should contain stages similar to:

```yaml
trigger:
- main

pool:
  vmImage: windows-latest

stages:
- stage: BuildAndTest
  jobs:
  - job: Backend
    steps:
    - task: UseDotNet@2
      inputs:
        version: 10.0.x
    - script: dotnet restore webapi-oyako/webapi-oyako.csproj
    - script: dotnet build webapi-oyako/webapi-oyako.csproj -c Release --no-restore
    - script: dotnet test webapi-oyako/webapi-oyako.Tests/webapi-oyako.Tests.csproj -c Release --no-build
  - job: Frontend
    steps:
    - task: NodeTool@0
      inputs:
        versionSpec: 22.x
    - script: npm ci
      workingDirectory: webapp-oyako
    - script: npm run build
      workingDirectory: webapp-oyako
    - script: npm run lint
      workingDirectory: webapp-oyako
    - script: npx playwright install chromium
      workingDirectory: webapp-oyako
    - script: npm run test:e2e
      workingDirectory: webapp-oyako
    - script: npm run test:a11y
      workingDirectory: webapp-oyako
```

Deployment stages should use Azure service connections, variable groups, environment approvals, and post-deploy health checks.

## 9. Quality and Security Gates

Recommended gates:

- Dependency audit for npm and NuGet.
- Secret scanning.
- Static analysis/SAST.
- License scanning.
- SBOM generation.
- Build artifact signing after pre-alpha.
- Playwright screenshot upload on failure.
- Health endpoint verification after deployment.

## 10. Observability and Post-Deploy Checks

Post-deploy checks should verify:

- `/api/api-health` is ready or ready_with_warnings.
- `/api/knowledge-health` reports database/cache/crawler state.
- `/api/health` aggregate status is acceptable.
- Frontend loads and status bar is not blank.
- Settings can read provider/model configuration.
- Knowledge Bank can list sources.
- A representative question returns an answer.

Logs should include backend runtime status events, crawl run summaries, ready question refresh results, AI provider availability, and frontend Playwright traces for failures.

## 11. Rollback and Recovery

Rollback should preserve SQLite data and filesystem storage. Oyako 1.0.0 treats the current schema as the primary contract, so schema-changing releases should be planned as explicit forward-only deployment steps.

Recommended rollback steps:

1. Stop new deployment traffic.
2. Restore previous backend/frontend artifacts.
3. Preserve or restore persistent SQLite file.
4. Re-check health endpoints.
5. Run smoke test.
6. Document incident and corrective action.

## 12. Operational Checklists

### Pull Request Checklist

- Backend builds and tests pass.
- Frontend builds and lint passes.
- Playwright e2e and Axe a11y pass.
- UI strings are Turkish.
- Code identifiers and comments are English.
- Docs updated when behavior changes.

### Main Branch Checklist

- Full CI green.
- Artifacts published.
- Smoke test runnable.
- No secret committed.

### Staging Checklist

- Deployment succeeded.
- Health endpoints verified.
- Knowledge source list verified.
- Representative question answered.
- Logs reviewed.

### Production Checklist

- Manual approval granted.
- Secrets configured.
- Persistent SQLite storage confirmed.
- Post-deploy smoke passed.
- Rollback path confirmed.

## 13. Recommended Future Enhancements

- Add strict contract tests between backend DTOs and frontend types.
- Add visual regression baselines after UI stabilizes.
- Add coverage thresholds after pre-alpha.
- Add nightly real-provider tests separated from PR mock tests.
- Add deployment-specific health dashboards.


## 14. Public GitHub and Azure ACA Release Flow

Release `2026.6.18.300` uses a public GitHub repository and minimal Azure deploy targets. The ACA release sequence is: sanitize source, initialize Git, create the detailed commit history, push to `github.com/hasan-ozdemir/oyako`, build one Docker image, push it as `oyako:latest`, deploy Container Apps, run public endpoint checks, and verify ACR contains only the latest image tag. The AWA sequence publishes ASP.NET with the React SPA under `wwwroot`, ZIP deploys one Web App, and runs the same public endpoint checks without Docker or ACR.

For pre-alpha simplicity, ACR image retention is intentionally aggressive. The `oyako` repository should not retain older tags after a successful deploy.
