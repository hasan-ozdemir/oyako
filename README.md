# Oyako

Oyako is a Turkish full-stack question-answer platform for curated knowledge sources. It combines a React + TypeScript + Vite web app with an ASP.NET 10 Web API, portable SQLite, source/document management, crawler/scraper workflows, knowledge-cache activation, ready-question generation, and switchable AI providers.

## Current Release

- Version: `2026.6.18.300`
- Frontend: React, TypeScript, Vite, PWA-ready SPA
- Backend: ASP.NET 10, Dapper, SQLite, Playwright-assisted scraping
- AI providers: Azure AI, Ollama Cloud, Ollama Local
- Container target: one full-stack Docker image
- Azure target: Azure Container Apps with ACR image `oyako:latest`

## Local Development

```powershell
.\run-app.cmd
```

The local script starts the backend on ports `5000` and `5001`, starts the frontend on port `3000`, and opens the default browser when the frontend is ready.

## Docker Development

```powershell
.\run-docker.cmd
```

The Docker flow builds the frontend and backend into one local container image and exposes the application on `http://localhost:3000` and the API through `/api`.

## Azure Container Apps Deployment

```powershell
.\deploy-aca.cmd
```

The deploy script uses Azure CLI, Docker Desktop, `azure-cloud.env`, `ollama-cloud.env`, and their `.example` templates. It builds and pushes `oyako:latest`, updates the Container App, verifies public endpoints, runs a Q&A smoke test unless skipped, and cleans ACR so the `oyako` repository keeps only `latest`.

## Secret Policy

Real `.env` files, SQLite databases, generated certificates, logs, raw uploaded data, and build artifacts are ignored. Public Git contains only source, tests, scripts, docs, and safe example configuration files.

## Documentation

- `docs/code-architecture.md`: backend, frontend, data flow, AI provider, and testing architecture.
- `docs/ci-cd-ct.md`: GitHub Actions and Azure DevOps CI/CD/CT blueprint.
- `docs/ui-production-readiness.md`: responsive, accessibility, and production-readiness checklist.
- `AGENTS.md`: practical operating rules for future Codex/agent work.

