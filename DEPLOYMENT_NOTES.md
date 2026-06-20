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

For cutover from the previous pre-alpha ACA script, `deploy-aca.cmd` removes only script-owned legacy names after the new deployment passes smoke tests. The default legacy names are `oyako-aca`, `aca-oyako-env`, and `acaoyako<subscription8>weacr`. It does not remove unrelated untagged resources or shared AI resources.

ACA default hostnames are Azure-managed and include the Container Apps Environment suffix:

`https://<container-app-name>.<environment-default-domain>/`

The script reads the active environment default domain at deploy time and expects the app hostname to match that Azure-managed value. With the current default app/environment names, the expected ACA hostname is `https://oyako.bluepond-a0ca5d32.italynorth.azurecontainerapps.io/`, with API traffic under `/api`. The shorter hostname `https://oyako.italynorth.azurecontainerapps.io/` is not an Azure Container Apps managed default hostname format. Use an ACA custom domain with owned DNS and certificate material if that exact public hostname is required.

`deploy-awa.cmd` manages only resources tagged with `app=oyako`, `managed-by=deploy-awa`, and `deployment-scope=oyako-awa`:

- Linux Basic App Service Plan
- Azure Web App

The scripts do not create Azure Storage Account, Static Web App, separate API App, Key Vault, Application Insights, Log Analytics Workspace, Redis, Cosmos DB, Azure SQL, PostgreSQL, MySQL, or another managed database resource.

## Deployment Naming

`oyako.env` is optional and ignored by Git. Use `oyako.env.example` as the safe template for tenant/app naming. If `oyako.env` is missing, both scripts use these defaults:

- `OYAKO_TENANT_SLUG=oyako`
- ACA app: `OYAKO_ACA_APP_NAME=oyako`
- ACA environment: `OYAKO_ACA_ENV_NAME=oyako-aca-env`
- AWA Web App: `OYAKO_AWA_WEBAPP_NAME=oyako`
- AWA App Service Plan: `OYAKO_AWA_PLAN_NAME=oyako-awa-plan`

`deploy-awa.cmd` checks App Service global name availability before local build/publish. If `oyako` is available, the target default hostname is `https://oyako.azurewebsites.net/`; otherwise the script fails before Azure mutation and the name must be changed in `oyako.env`.

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

- ACA uses the ASP.NET runtime image, installs Playwright Chromium and OS dependencies at image build time, and then runs the smallest tested CPU/memory profile that passes frontend, `/health`, and `/health/browser`.
- AWA uses Azure's built-in Linux App Service runtime. The script publishes the backend for `linux-x64`, includes the Linux Playwright driver assets, writes an LF-only `startup.sh`, installs Playwright OS dependencies when the built-in image lacks them, installs Chromium into `/home/oyako-playwright/ms-playwright`, then requires `/health/browser` to pass. If this chain fails, the script stops instead of silently producing a degraded deployment.

## NuGet Audit Mode

The backend app and test projects use `NuGetAuditMode=direct`. The current Microsoft.Data.Sqlite package graph resolves the latest available `SQLitePCLRaw.lib.e_sqlite3` package as a transitive dependency, and NuGet currently reports a transitive advisory for that package. Direct package references remain audited, while deployment/build/test output stays warning-free for the strict scripts. Revisit this when Microsoft.Data.Sqlite or SQLitePCLRaw publishes a fixed transitive package set.

## Log Analytics

`deploy-aca.cmd` creates the Container Apps Environment with `--logs-destination none` to avoid provisioning a Log Analytics Workspace. If a future Azure CLI/API version requires logs, the script should be updated explicitly and this note must be revised.
