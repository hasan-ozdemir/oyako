# Oyako Deployment Notes

## Target Scope

- Azure subscription: `az2vs`
- Azure resource group: `rg-oyako`
- Azure location: `italynorth`
- Image policy for ACA: one repository/tag, `oyako:latest`

## Minimal Azure Resources

`deploy-aca.cmd` manages only resources tagged with `app=oyako`, `managed-by=deploy-aca`, and `deployment-scope=oyako-aca`:

- Azure Container Registry Basic SKU
- Azure Container Apps Environment
- Azure Container App with one always-on replica

`deploy-awa.cmd` manages only resources tagged with `app=oyako`, `managed-by=deploy-awa`, and `deployment-scope=oyako-awa`:

- Linux Basic App Service Plan
- Azure Web App

The scripts do not create Azure Storage Account, Static Web App, separate API App, Key Vault, Application Insights, Log Analytics Workspace, Redis, Cosmos DB, Azure SQL, PostgreSQL, MySQL, or another managed database resource.

## Database Strategy

Oyako uses portable SQLite and bootstraps the schema at application startup. The deployment scripts keep SQLite as an app-local file:

- ACA: `/app/data/oyako.sqlite`
- AWA: `/home/oyako-data/oyako.sqlite`

This satisfies the no-external-DB rule, but ACA container filesystem data is not a durable database strategy across aggressive recreation, image replacement, or instance loss. The ACA script is intentionally cutover-oriented and may reset local SQLite state when it recreates the Container App or Environment. The AWA path uses App Service `/home`, which is more stable for app-local files, but it is still not a separately managed database service.

## Secrets and Environment Files

Both Azure deployment scripts fail fast if required cloud env files or required keys are missing:

- `azure-cloud.env`: `AzureAi__Endpoint`, `AzureAi__DeploymentName`, `AzureAi__Deployments__0`, `AzureAi__ApiVersion`, `AzureAi__ApiKey`
- `ollama-cloud.env`: `ollama_api_key`

These files are local-only and ignored by Git. The scripts do not invent secrets.

## Playwright and Browser Health

The backend includes `/health/browser` so deployments can verify that Chromium-backed scraping works.

- ACA uses the Playwright .NET runtime image and then picks the smallest tested CPU/memory profile that passes frontend, `/health`, and `/health/browser`.
- AWA uses Azure's built-in Linux App Service runtime. The script publishes the backend for `linux-x64`, includes the Linux Playwright driver assets, writes an LF-only `startup.sh`, installs Playwright OS dependencies when the built-in image lacks them, installs Chromium into `/home/oyako-playwright/ms-playwright`, then requires `/health/browser` to pass. If this chain fails, the script stops instead of silently producing a degraded deployment.

## NuGet Audit Mode

`webapi-oyako.csproj` uses `NuGetAuditMode=direct`. The current Microsoft.Data.Sqlite package graph resolves the latest available `SQLitePCLRaw.lib.e_sqlite3` package as a transitive dependency, and NuGet currently reports a transitive advisory for that package. Direct package references remain audited, while deployment/build output stays warning-free for the strict scripts. Revisit this when Microsoft.Data.Sqlite or SQLitePCLRaw publishes a fixed transitive package set.

## Log Analytics

`deploy-aca.cmd` creates the Container Apps Environment with `--logs-destination none` to avoid provisioning a Log Analytics Workspace. If a future Azure CLI/API version requires logs, the script should be updated explicitly and this note must be revised.
