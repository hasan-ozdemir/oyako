# Oyako Agent Guide

## Mission
Oyako is a Turkish full-stack question-answer platform. It uses a React + TypeScript + Vite SPA and an ASP.NET 10 Web API with portable SQLite. The assistant answers from enabled knowledge sources and documents, using one-shot AI calls through Azure, Ollama Cloud, or Ollama Local depending on configuration.

## Repository Map
- `webapi-oyako/`: ASP.NET backend, Domain/Application/Infrastructure/Presentation layers, SQLite bootstrap, crawler/scraper, AI providers, tests.
- `webapp-oyako/`: React SPA/PWA, Turkish UI, accessibility-first dialogs, source/document management, Playwright tests.
- `docs/`: architecture, CI/CD/CT, and UI production-readiness notes.
- `Dockerfile`, `run-docker.cmd`, `deploy-aca.cmd`, `deploy-awa.cmd`: single-host/container automation, Azure Container Apps, and direct Azure Web App deployment.
- `run-app.cmd`: local backend/frontend launcher.

## Language Rules
- Code identifiers, comments, API contracts, class names, method names, and filenames stay English.
- User-facing UI strings, help text, runtime status labels, and assistant-facing Turkish product instructions stay Turkish.
- Do not add legacy compatibility paths at all. The newest requested behavior is the primary behavior.

## Safety Rules
- Never commit real secrets: `*.env`, `.tenants/*.env`, certificates, SQLite files, logs, runtime data, or uploaded raw files.
- Commit only safe template files such as `.tenants/.template.env.example`; do not reintroduce root provider env examples unless the user explicitly asks.
- Keep `webapi-oyako/Data/`, `.certificates/`, `node_modules/`, `dist/`, `bin/`, `obj/`, and Playwright artifacts out of Git.
- Before public push, scan staged content for API keys, private keys, connection strings, and generated data.

## Backend Conventions
- Preserve Clean Architecture boundaries: Domain has contracts and core vocabulary; Application coordinates workflows; Infrastructure owns SQLite/HTTP/browser/AI implementations; Presentation exposes minimal APIs.
- SQLite must bootstrap itself code-first in an empty environment.
- Knowledge source/document mutations must immediately update active knowledge cache when they affect enabled, non-archived content.
- Enable/disable and archive/unarchive actions should be fast cache state switches, not heavy redownload operations.
- Website crawling must use bounded timeouts, random request delay, and fail-forward behavior.
- Web links and local files share the same parse/clean/normalize pipeline where possible.
- AI calls for Q&A remain one-shot: system instruction plus one user message, no provider-side chat history.

## Frontend Conventions
- All popups are modal and accessible. They must trap focus, close predictably, and restore focus.
- Dialog action order should place the primary action before cancel where requested.
- Text inputs and textareas must not lose focus or close dialogs while typing.
- Scrollbars should appear only when content exceeds the available area; no content should become unreachable.
- Status bar must never be blank. Use `Sayfa Yükleniyor`, `Uygulama Hazır`, `Soru Soruluyor`, or the best contextual Turkish status.
- Tables use external semantic headings, not table captions as visible section titles.

## Test Commands
- Backend: `dotnet test webapi-oyako/webapi-oyako.Tests/webapi-oyako.Tests.csproj`
- Backend build: `dotnet build webapi-oyako/webapi-oyako.csproj`
- Frontend install: `npm ci --prefix webapp-oyako`
- Frontend build: `npm run build --prefix webapp-oyako`
- Frontend lint: `npm run lint --prefix webapp-oyako`
- Frontend UI tests: `npm run test:ui --prefix webapp-oyako`

## Docker and Azure
- Local Docker can include local-development behavior.
- Azure Container Apps image must include Azure and Ollama Cloud providers; Ollama Local is disabled in Azure.
- `deploy-aca.cmd` deploys one image: `<tenant_name>-<tenant_order_number>:latest`.
- `deploy-awa.cmd` deploys the locally published ASP.NET app and embedded React SPA directly to one Linux Azure Web App without Docker or ACR.
- Azure ACR should retain only the latest `<tenant_name>-<tenant_order_number>:latest` image for this pre-alpha flow.
- Azure deploy target uses subscription `az2vs`, location `italynorth`, and per-tenant resource groups named `rg-<tenant_id>-<tenant_order_number>`.
- `rg-oyako` is reserved for Azure AI/Cognitive Services only; app, ACR, ACA, Web App, and App Service Plan resources must not be created there.
- Tenant discovery is `.tenants/*.env` traversal. Do not add code-level tenant allow-lists; use `tenant_enabled=true` in the tenant env file to activate a tenant.

## Documentation Discipline
- Update README and docs whenever public behavior, script usage, deploy flow, API behavior, or user workflow changes.
- Keep help content Turkish and aligned with the latest UI.
- Prefer concise, explicit comments that explain purpose and workflow decisions.

## Commit Discipline
- After every user prompt, if any code or file change is made, analyze all changes immediately before giving the user a summary.
- Split changes into the smallest logical parts and record each part as a separate atomic git commit.
- Commit messages must be contextual, developer-friendly, detailed, and descriptive.
- Do not combine independent changes into a single commit.
- Inspect `git diff` before committing and avoid committing temporary files, generated artifacts, local secrets, runtime data, or unrelated changes.
- If tests, builds, or deploys were attempted, report their result in the final response and include the relevant context in commit messages when useful.
