# Oyako Code Architecture

## 1. Product Purpose

Oyako is a full-stack, one-page question-answer platform for tenant-managed knowledge sources. The frontend gives users a modern Turkish UI for asking questions. The backend collects enabled tenant web content, stores it in portable SQLite, builds a system instruction cache, generates ready questions, and sends one-shot prompts to the active AI provider.

The product rule is simple: code identifiers and developer comments stay in English, while user-facing UI, runtime messages, assistant instructions, and help content stay Turkish.

## 2. Repository Layout

The repository has two primary applications and shared operational files.

- `webapi-oyako`: ASP.NET backend, domain logic, data access, crawler/scraper, AI provider routing, runtime status, and tests.
- `webapp-oyako`: React + TypeScript + Vite frontend, SPA/PWA UI, API client, Playwright/Axe tests, and visual system.
- `docs`: architecture, CI/CD/CT, and engineering documentation.
- `run-app.cmd`: local automation that restarts and runs backend and frontend together.
- `Dockerfile`: production single-process image that serves the React SPA from ASP.NET `wwwroot` on port `8080`.
- `deploy-aca.cmd` and `deploy-awa.cmd`: minimal Azure deployment scripts for Container Apps and direct Linux Web App hosting.
- `.tenants/<tenant-name>.env`: local tenant configuration source for branding, tenant IDs, Azure names, AI provider defaults, crawler limits, and SQLite path.
- `.tenants/.template.env.example`: the only committed tenant env template; copy it to a real ignored tenant file when adding a tenant.
- `azure-cloud.env` and `ollama-cloud.env`: local-only cloud provider secret sources.

Generated folders such as `bin`, `obj`, `node_modules`, `dist`, and Playwright artifacts are not part of source architecture.

## 3. Backend Architecture

The backend follows Clean Architecture and N-tier layering. Each layer owns a clear responsibility and depends inward through interfaces.

### 3.1 Domain Layer

The Domain layer contains the stable vocabulary of the application. It defines entities, models, repository contracts, service contracts, and enums.

- Entities describe persisted business data such as web pages, crawl runs, ready questions, and system instruction cache entries.
- Models describe immutable transfer shapes such as runtime status snapshots, AI provider statuses, chat answer snapshots, and knowledge refresh results.
- Repository interfaces define persistence capabilities without exposing SQLite implementation details.
- Service interfaces define behavior contracts such as crawling, chat, runtime status, ready question refresh, system instruction cache, and AI configuration.

The Domain layer should not know about ASP.NET, SQLite, Playwright, Azure, Ollama, Dapper, or React.

### 3.2 Application Layer

The Application layer coordinates business workflows.

- `ChatService` builds the current system prompt, streams AI tokens, sanitizes markdown into HTML, and publishes runtime status.
- `ChatPromptBuilder` converts stored web pages into strict Turkish system instructions that constrain hallucination and require suggested questions.
- `SystemInstructionCache` loads, invalidates, refreshes, and persists the assembled system instruction cache.
- `KnowledgeRefreshService` executes the full non-destructive refresh workflow: backup, clear, crawl, persist, cache refresh, ready question generation, cleanup, and restore on failure.
- `CrawlerCoordinator` runs incremental background crawling and cache refresh without the full destructive refresh flow.
- `ReadyQuestionService` generates and rotates LLM-generated ready questions based on current knowledge content.
- `KnowledgeTextCleaner` removes repeated navigation/footer/client-error boilerplate from scraped content and previews.
- `AnswerHtmlSanitizer` converts assistant markdown to safe HTML and extracts suggested questions.
- `RuntimeStatusService` stores and streams structured runtime status for UI progress and live status messages.
- `AiConfigurationService` persists and resolves selected AI provider/model settings.

The Application layer contains orchestration, validation, safety rules, and user-visible Turkish messages. It depends on Domain contracts and is implemented by Infrastructure services.

### 3.3 Infrastructure Layer

The Infrastructure layer connects the application to external systems.

- SQLite repositories use Dapper and `Microsoft.Data.Sqlite` to persist pages, crawl runs, cache entries, ready questions, and AI settings.
- `SqliteDbInitializer` creates required portable database tables and indexes at startup.
- `HtmlCrawler` discovers pages through robots, sitemap, HTML links, rendered links, and utility documents.
- `PlaywrightPageRenderer` runs lightweight Chromium rendering to collect browser-visible text and links.
- `RenderedTextExtractor` extracts text and links from rendered HTML content paths.
- `AzureAiClient` calls Azure AI chat completions and parses streaming/non-streaming responses.
- `OllamaClient` calls local Ollama on port 11434 and preserves one-shot system/user prompt behavior.
- `AiProviderRouter` selects the active provider and exposes provider/model availability.
- `EnvFileLoader` loads cloud provider env files and the selected `.tenants/<tenant-name>.env` into the process environment, including indexed `tenant_knowledge_source_N_*` seed sources.
- `CrawlBootstrapHostedService` starts tenant seed-source crawling when the web API starts.

Infrastructure is the only layer that knows concrete frameworks, database drivers, browser automation, HTTP clients, and AI provider protocols.

### 3.4 Presentation Layer

The Presentation layer exposes HTTP endpoints through minimal APIs.

Important endpoints include:

- `/api/chat/stream`: streams one-shot answers and suggested questions.
- `/api/knowledge/sources`: returns cleaned knowledge source metadata and previews.
- `/api/knowledge-refresh`: runs the full backup-refresh-cache-ready-question workflow.
- `/api/runtime/status`: returns the latest structured runtime status.
- `/api/runtime/status/stream`: streams structured status over Server-Sent Events.
- `/api/ready-questions`: returns generated ready questions.
- `/api/ai-settings`: reads and updates active provider/model settings.
- `/api/knowledge-refresh-settings`: reads and updates the tenant seed website refresh cadence.
- `/api/api-health`, `/api/knowledge-health`, `/api/health`: report API, knowledge, and aggregate health.

The runtime status contract is structured around `operation`, `phase`, `stepKey`, `stepIndex`, `stepCount`, `isTerminal`, `message`, `severity`, `icon`, `pageCount`, and `updatedAtUtc`. The frontend uses this for live status and ProgressView behavior.

## 4. Backend Data Flow

### 4.1 Startup

At startup, the API loads the selected tenant environment, validates required tenant and seed-source values, initializes SQLite schema, registers services, starts hosted refresh workers, and exposes minimal API endpoints.

### 4.2 Crawling and Scraping

The crawler discovers URLs for the active tenant seed website from robots, sitemap, direct root URLs, HTML links, and rendered page links. Playwright renders pages like a browser, waits for stabilization, captures `document.body.innerText`, and extracts links.

The crawler respects delay settings, avoids unnecessary third-party resources, and stores only valid text above the configured minimum length.

### 4.3 Text Cleaning

`KnowledgeTextCleaner` removes repeated navigation menus, footer text, legal boilerplate, client-side error messages, and duplicate short lines. This improves both the UI preview and the AI knowledge context.

### 4.4 Persistence

Cleaned pages are stored in tenant-local SQLite with URL, title, content, content hash, first seen time, last seen time, and last crawl time. Env-managed seed website refreshes use staged replacement with backup/restore protection; admin-added sources and documents remain tenant-local and separate from other tenants.

### 4.5 System Instruction Cache

The cache reads all current pages, builds a full Turkish system instruction, stores the assembled prompt in SQLite, and keeps an in-memory snapshot for fast chat requests. Cache invalidation happens when source content changes or full refresh is requested.

### 4.6 Ready Questions

Ready questions are generated by the active AI provider from the current tenant-only knowledge payload. The table is replaced as a single current set whenever source fingerprint changes or a force refresh runs. The frontend receives a small rotating subset.

### 4.7 Chat Answering

Each chat request is one-shot. The backend sends exactly one system instruction and one user question to the active provider. It does not send previous chat history to the LLM. The frontend may display a conversation-like visual history, but backend inference remains no-history.

## 5. Frontend Architecture

The frontend is a React + TypeScript + Vite SPA.

### 5.1 App Shell

`App.tsx` owns the primary user experience:

- Header with new chat, tenant brand link, visitor menu, and settings.
- Question entry panel with Turkish welcome copy and keyboard shortcuts.
- Ready questions panel populated by backend-generated questions.
- Q&A board with accessible `Siz` and tenant assistant headings.
- Latest suggested questions shown only after the latest assistant answer.
- Status bar with live runtime status, knowledge source count, warning message, and utility menu.
- Knowledge Bank dialog with source list and knowledge refresh action.
- Settings dialog with AI provider/model selection, tenant seed-source refresh cadence, and save progress.
- Help dialog rendered through `HelpPage.tsx`.

### 5.2 API Client

`services/chatApi.ts` centralizes backend calls. It normalizes network failures into Turkish user-facing errors and exposes typed functions for health, runtime status, sources, refresh, settings, ready questions, and chat streaming.

### 5.3 Types

`types/chat.ts` defines frontend contracts matching backend DTOs. The most important contract is `RuntimeStatus`, which drives live UI progress.

### 5.4 Visual System

`index.css` defines global theme tokens, typography, focus behavior, and base layout. `App.css` defines component-level styling, responsive layout, dialogs, progress views, cards, bubbles, menus, and status bar behavior.

The UI follows a Turkish product language, tenant-neutral red/neutral color system, strong focus-visible support, and mobile-first responsive corrections.

### 5.5 Help Page

`HelpPage.tsx` is an exhaustive Turkish user guide. It explains the product, one-shot AI behavior, Knowledge Bank, refresh flow, ready questions, settings, live status, accessibility, PWA/responsive usage, menus, and answer verification.

## 6. Frontend Data Flow

1. The app loads with `Sayfa Yükleniyor` status.
2. It fetches knowledge health, ready questions, and runtime status.
3. It opens an SSE stream for live runtime status; polling covers refresh progress if SSE is unavailable.
4. The user submits a question through Enter or the Sor button.
5. The frontend sends one chat stream request to the backend.
6. The backend streams sanitized HTML answer snapshots and suggested questions.
7. The frontend updates only the latest assistant message and latest suggested questions section.
8. The status bar returns to `Hazır` when the backend reports readiness.

## 7. Knowledge Refresh UI Flow

The Knowledge Bank dialog includes a `Bilgileri yenile` action. When clicked:

1. The frontend starts local ProgressView state.
2. The backend runs full knowledge refresh.
3. Runtime status provides step metadata for all nine steps.
4. The ProgressView shows reached count, active step, checkmarks, and completion.
5. A Turkish a11y alert announces success or failure.
6. The user can click `Tamam, teşekkürler`, or the view auto-closes after 10 seconds.
7. Sources, health, and ready questions are reloaded.

## 8. Settings Flow

The Settings dialog reads provider/model options from the backend. User changes are stored only when the user presses Geri with changes pending. During save, a ProgressView announces `Değişiklikler Kaydediliyor`; on success it announces `Değişiklikler Kaydedildi` and closes/clears the progress view.

## 9. Testing Architecture

### 9.1 Backend Tests

Backend tests cover URL normalization, text extraction, crawler behavior, cache behavior, ready question generation, AI provider clients, runtime status, data maintenance, and sanitizer behavior.

### 9.2 Frontend Tests

Playwright tests use a mock API to validate UI behavior without depending on Azure, Ollama, or the live Tenant Demo site. Axe tests check critical accessibility issues on the main screen and dialogs.

### 9.3 Full-Stack Tests

Full-stack validation starts backend and frontend, checks health endpoints, optionally runs knowledge refresh, verifies sources and ready questions, and asks a real question through the UI.

## 10. Configuration and Operations

- Local development ports: backend 5000/5001 and frontend 3000.
- Single-host/container port: 8080.
- SQLite is portable and hosted by the web API. Azure deploy scripts keep it as a local file inside the app/container and do not create external managed database resources.
- `.tenants/<tenant-name>.env` supplies tenant-specific runtime configuration; cloud provider secrets stay in ignored provider env files.
- `.tenants/.template.env.example` documents every required tenant key. A new tenant starts by copying this template, aligning the filename with `tenant_name`, setting tenant-specific branding and knowledge-source values, then enabling it with `tenant_enabled=true`.
- `azure-cloud.env` and `ollama-cloud.env` supply cloud deployment secrets at deploy time and must not be committed.
- Azure/Ollama Cloud are deployment-capable providers; Ollama Local is disabled for Azure-hosted runs.
- Knowledge refresh may call external web resources and AI providers, so tests should distinguish fast mock tests from slower special tests.

## 11. Engineering Conventions

- Code identifiers and comments are English.
- User-facing UI strings are Turkish.
- System instructions are Turkish because the assistant responds to Turkish users and Turkish source content.
- JSON and lock files are not commented because comments would invalidate them.
- Generated/vendor artifacts are not maintained manually.
- New features should add backend tests, frontend tests, and documentation updates.

## 12. Extension Guide

To add a backend feature, start with Domain contracts, implement Application orchestration, add Infrastructure adapters if needed, expose Presentation endpoints, and add tests.

To add a frontend feature, add or update typed API calls, update state flow in `App.tsx` or a focused component, style with existing tokens, add a11y semantics, and add Playwright/Axe coverage.

To add a new AI provider, implement provider client behavior, register it in dependency injection, expose models through provider catalog, and test routing plus settings persistence.

To add new knowledge source behavior, update crawler/cleaner/store/cache flow together so DB content, system instruction cache, ready questions, and UI preview remain consistent.


## 14. Release 2026.6.22.343 Notes

This release is the public pre-alpha baseline. It treats the current schema and APIs as the primary behavior, excludes runtime state from source control, and publishes with a synthetic but evidence-driven Git history built from Codex session messages, assistant summaries, tool calls, tool outputs, code state, and documentation state.

The Azure deployment paths are intentionally minimal:

- `deploy-aca.cmd` builds and pushes `<tenant_name>-<tenant_order_number>:latest`, recreates only ACA-scope tagged resources in `italynorth`, deploys one always-on Container App, verifies public health/browser behavior, and confirms the ACR repository contains only the `latest` tag.
- `deploy-awa.cmd` locally publishes ASP.NET for `linux-x64` with the React SPA in `wwwroot`, deploys one ZIP to a single Linux Azure Web App on a Basic always-on App Service Plan, installs Playwright runtime dependencies at startup, and verifies public health/browser behavior without Docker or ACR.
