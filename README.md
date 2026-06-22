# Oyako

Oyako is a Turkish full-stack question-answer platform for curated knowledge sources. It combines a React + TypeScript + Vite web app with an ASP.NET 10 Web API, portable SQLite, source/document management, crawler/scraper workflows, knowledge-cache activation, ready-question generation, and switchable AI providers.

## Current Release

- Version: `2026.6.22.343`
- Frontend: React, TypeScript, Vite, PWA-ready SPA
- Backend: ASP.NET 10, Dapper, SQLite, Playwright-assisted scraping
- AI providers: Azure AI, Ollama Cloud, Ollama Local
- Container target: one full-stack Docker image on port `8080`
- Azure targets: Azure Container Apps with ACR image `<tenant_name>-<tenant_order_number>:latest`, or one Linux Azure Web App with direct ZIP deploy

## Local Development

```powershell
.\run-app.cmd
```

The local script discovers tenants by traversing `.tenants/*.env`, resolves the default tenant from tracked `oyako.env`, requires `tenant_enabled=true`, starts the backend on ports `5000` and `5001`, starts the frontend on port `3000`, and opens the default browser when the frontend is ready. Use `.\run-app.cmd --tenant-name generictenant` to run another tenant locally.

Tenant env files also seed the baseline website knowledge source for that tenant through `tenant_knowledge_source_1_type`, `tenant_knowledge_source_1_url`, and `tenant_knowledge_source_1_refresh_period`. Background refresh applies only to these env-managed seed website sources; admin-added sources and documents stay tenant-local and are preserved.

## Tenant Configuration

Tenant discovery is file-based. Copy `.tenants/.template.env.example` to `.tenants/<tenant-name>.env`, then set `tenant_name` to the same `<tenant-name>` value. Keep the copied file ignored by Git. The root `oyako.env` file is the committed, secrets-free global config source for tenant-agnostic defaults such as `default_tenant_id`, Azure location, App Service SKU, and release readiness settings.

For a new tenant, fill in the tenant identity, Azure DNS name, optional custom domain, public brand URL, admin/feedback email addresses, AI provider defaults, website seed source, text-cleaner terms, SQLite path, and every `ui_web_*` branding string. Set `tenant_enabled=true` only after the tenant file is complete. The scripts reject disabled tenants and files whose name does not match `tenant_name`.

Tenant brand logos are expected under `webapp-oyako/public/tenants/<tenant-name>/brand-logo.svg` when `ui_web_brand_logo_url=/tenants/<tenant-name>/brand-logo.svg` is used.

## Docker Development

```powershell
.\run-docker.cmd
```

The Docker flow builds the frontend and backend into one local container image and exposes the application and API on `http://localhost:8080`. It requires `azure-cloud.env` and `ollama-cloud.env` for cloud-provider configuration.

## Azure Container Apps Deployment

```powershell
.\deploy-aca.cmd
```

The Container Apps script uses Azure CLI, Docker Desktop, `azure-cloud.env`, `ollama-cloud.env`, and a discovered enabled `.tenants/<tenant>.env`. It targets subscription `az2vs`, tenant resource group `rg-<tenant_id>-<tenant_order_number>`, and `italynorth`; creates deterministic ACR `acr<tenant_order_number><tenant_id>`; builds and pushes only `<tenant_name>-<tenant_order_number>:latest`; and verifies the ACR contains exactly one repository and one `latest` tag. Pass `--tenant-name <tenant>` or `-t <tenant>`; otherwise the default tenant is resolved from `oyako.env`.

Use `.\deploy-aca.cmd --tenant-name <tenant> --local-image-only` to validate tenant config and build the local Docker image without Azure login, ACR push, or Container Apps mutation.

## Azure Web App Deployment

```powershell
.\deploy-awa.cmd
```

The Web App script publishes the ASP.NET API for Linux with the React SPA copied into `wwwroot`, then deploys the ZIP to one Linux Azure Web App on a Basic always-on App Service Plan. It installs Playwright Chromium dependencies at startup so `/health/browser` can pass without Docker or ACR. It uses the same `az2vs` / per-tenant resource group / `italynorth` target and does not create ACR, Azure Storage, Key Vault, Application Insights, or an external managed database. Pass `--tenant-name <tenant>` or `-t <tenant>`; otherwise the default tenant is resolved from `oyako.env`. Use `--package-only` to build and validate the deployment ZIP without touching Azure.

## GitHub Actions Release

Pushing to `main` runs `.github/workflows/release-awa.yml`. The workflow logs into Azure with the `AZURE_CREDENTIALS` service-principal secret, recreates ignored env files from GitHub Secrets, runs `deploy-awa.cmd` for the default tenant, and then passively waits for startup knowledge readiness through `/api/knowledge-health` and `/api/ready-questions`. It does not explicitly trigger an extra crawler refresh.

Custom domain binding is optional in both deploy scripts. If DNS or Azure hostname binding is not ready, the script reports a warning and continues with the Azure-managed hostname.

## Secret Policy

Real secret `.env` files, including `.tenants/*.env`, provider env files, SQLite databases, generated certificates, logs, raw uploaded data, and build artifacts are ignored. The committed `oyako.env` file is intentionally secrets-free.

## Documentation

- `docs/code-architecture.md`: backend, frontend, data flow, AI provider, and testing architecture.
- `docs/ci-cd-ct.md`: GitHub Actions and Azure DevOps CI/CD/CT blueprint.
- `docs/DEPLOYMENT_NOTES.md`: Azure deployment, tenant resource naming, and no-external-storage/database notes.
- `docs/ui-production-readiness.md`: responsive, accessibility, and production-readiness checklist.
- `AGENTS.md`: practical operating rules for future Codex/agent work.

