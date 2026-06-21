# Oyako

Oyako is a Turkish full-stack question-answer platform for curated knowledge sources. It combines a React + TypeScript + Vite web app with an ASP.NET 10 Web API, portable SQLite, source/document management, crawler/scraper workflows, knowledge-cache activation, ready-question generation, and switchable AI providers.

## Current Release

- Version: `2026.6.18.300`
- Frontend: React, TypeScript, Vite, PWA-ready SPA
- Backend: ASP.NET 10, Dapper, SQLite, Playwright-assisted scraping
- AI providers: Azure AI, Ollama Cloud, Ollama Local
- Container target: one full-stack Docker image on port `8080`
- Azure targets: Azure Container Apps with ACR image `oyako:latest`, or one Linux Azure Web App with direct ZIP deploy

## Local Development

```powershell
.\run-app.cmd
```

The local script reads `.tenants/oyakdijital.env` by default, starts the backend on ports `5000` and `5001`, starts the frontend on port `3000`, and opens the default browser when the frontend is ready. Use `.\run-app.cmd --tenant-name generictenant` to run another tenant locally.

## Docker Development

```powershell
.\run-docker.cmd
```

The Docker flow builds the frontend and backend into one local container image and exposes the application and API on `http://localhost:8080`. It requires `azure-cloud.env` and `ollama-cloud.env` for cloud-provider configuration.

## Azure Container Apps Deployment

```powershell
.\deploy-aca.cmd
```

The Container Apps script uses Azure CLI, Docker Desktop, `azure-cloud.env`, `ollama-cloud.env`, and `.tenants/<tenant>.env`. It targets subscription `az2vs`, tenant resource group `rg-<tenant_id>-<tenant_order_number>`, and `italynorth`; builds and pushes only `oyako:latest`; creates or recreates only resources tagged for the ACA scope; and keeps the ACR `oyako` repository limited to `latest`. Pass `--tenant-name <tenant>` or `-t <tenant>`; the default is `oyakdijital`.

## Azure Web App Deployment

```powershell
.\deploy-awa.cmd
```

The Web App script publishes the ASP.NET API for Linux with the React SPA copied into `wwwroot`, then deploys the ZIP to one Linux Azure Web App on a Basic always-on App Service Plan. It installs Playwright Chromium dependencies at startup so `/health/browser` can pass without Docker or ACR. It uses the same `az2vs` / per-tenant resource group / `italynorth` target and does not create ACR, Azure Storage, Key Vault, Application Insights, or an external managed database. Pass `--tenant-name <tenant>` or `-t <tenant>`; the default is `oyakdijital`.

## Secret Policy

Real `.env` files, including `.tenants/*.env`, SQLite databases, generated certificates, logs, raw uploaded data, and build artifacts are ignored. Public Git contains only source, tests, scripts, docs, and safe example configuration files.

## Documentation

- `docs/code-architecture.md`: backend, frontend, data flow, AI provider, and testing architecture.
- `docs/ci-cd-ct.md`: GitHub Actions and Azure DevOps CI/CD/CT blueprint.
- `docs/ui-production-readiness.md`: responsive, accessibility, and production-readiness checklist.
- `AGENTS.md`: practical operating rules for future Codex/agent work.

