# Oyako Deployment Notes

## Target Scope

- Azure subscription: `az2vs`
- Azure resource group: per tenant, `rg-<tenant_id>-<tenant_order_number>`
- Azure location: `italynorth`
- Image policy for ACA: one repository/tag, `<tenant_name>-<tenant_order_number>:latest`

## Minimal Azure Resources

`deploy-aca.cmd` manages only resources tagged with `app=oyako`, `managed-by=deploy-aca`, and `deployment-scope=oyako-aca`:

- Azure Container Registry Basic SKU
- Azure Container Apps Environment
- Azure Container App with one always-on replica

Application resources must not be deployed to `rg-oyako`; that resource group is reserved for shared Azure AI/Cognitive Services resources. The one-time cutover removes non-AI application resources from `rg-oyako` while preserving `Microsoft.CognitiveServices/*`.

ACA default hostnames are Azure-managed and include the Container Apps Environment suffix:

`https://<container-app-name>.<environment-default-domain>/`

The script reads the active environment default domain at deploy time and expects the app hostname to match that Azure-managed value. The container app name is tenant-driven from `tenant_azure_domain_name`, so the default managed hostname becomes `https://<tenant_azure_domain_name>.<environment-default-domain>/`, with API traffic under `/api`. The shorter hostname `https://<tenant_azure_domain_name>.italynorth.azurecontainerapps.io/` is not an Azure Container Apps managed default hostname format. Use an ACA custom domain with owned DNS and certificate material if that exact public hostname is required.

`deploy-awa.cmd` manages only resources tagged with `app=oyako`, `managed-by=deploy-awa`, and `deployment-scope=oyako-awa`:

- Linux Basic App Service Plan
- Azure Web App

The AWA script uses Azure CLI `webapp deploy --type zip --clean true` for the ZIP upload path. By default `awa_recreate_webapp=true` in `oyako.env`, so each release deletes and recreates only the script-owned Web App before deployment while keeping the script-owned App Service Plan when it is already compliant. This clears old Kudu manifests and `wwwroot` artifacts, which is required after pre-cutover Windows-style ZIP paths polluted the previous App Service deployment manifest.

`awa_scm_settle_seconds` controls the wait after App Service configuration changes before ZIP deployment. `awa_deploy_timeout_milliseconds` controls the Azure CLI OneDeploy timeout.

The scripts do not create Azure Storage Account, Static Web App, separate API App, Key Vault, Application Insights, Log Analytics Workspace, Redis, Cosmos DB, Azure SQL, PostgreSQL, MySQL, or another managed database resource.

## Tenant Naming and Lifecycle

Tenant configuration is discovered by traversing `.tenants/*.env`. The real tenant `.env` files are ignored by Git; the only committed tenant template is `.tenants/.template.env.example`. If a script is run without `--tenant-name` or `-t`, it resolves the default tenant from committed, secrets-free `oyako.env` using `default_tenant_id`, then `default_tenant_name`, then the hard-coded final fallback `oyakdijital`.

Required tenant keys include:

- `tenant_id`: 32 lowercase hex characters.
- `tenant_order_number`: positive integer, starting at `1` per tenant identity.
- `tenant_name`: must match the selected `<tenant-name>`.
- `tenant_enabled`: must be `true` for run/deploy; missing or `false` means disabled.
- `tenant_azure_domain_name`: used as the Azure Web App name and ACA app name.
- `tenant_custom_domain_name`: optional custom hostname. Scripts warn and continue if DNS or Azure hostname binding is not ready.

Resource groups are tenant-scoped: `rg-<tenant_id>-<tenant_order_number>`.
ACA registry names are tenant-scoped and deterministic: `acr<tenant_order_number><tenant_id>`.
ACA image repositories are tenant-scoped and deterministic: `<tenant_name>-<tenant_order_number>:latest`.

`deploy-awa.cmd` checks App Service global name availability before local build/publish. If `<tenant_azure_domain_name>` is unavailable or belongs to an unowned app, the script fails before Azure mutation and the tenant env file must be changed or the name reclaimed in Azure.

New tenant lifecycle:

1. Copy `.tenants/.template.env.example` to `.tenants/<tenant-name>.env`.
2. Set `tenant_name=<tenant-name>` and keep the file name and value identical.
3. Replace the placeholder `tenant_id` with a unique 32-character lowercase hex value.
4. Set `tenant_order_number=1` for the first environment of that tenant identity; increment only for separate tenant-order deployments.
5. Set `tenant_display_name`, `tenant_web_url`, `tenant_admin_email`, `tenant_feedback_email`, and all `ui_web_*` strings.
6. Set `tenant_azure_domain_name` to the desired Azure Web App / ACA app DNS label and verify it is globally available before live deploy.
7. Set `tenant_custom_domain_name` only when an owned DNS name exists; scripts warn and continue if CNAME/binding is not ready.
8. Add `webapp-oyako/public/tenants/<tenant-name>/brand-logo.svg` when the tenant logo URL points to the local brand asset path.
9. Configure `tenant_knowledge_source_1_*`, crawler limits, text-cleaner terms, AI provider defaults, and SQLite path.
10. Set `tenant_enabled=true`, then validate locally with `run-app.cmd --tenant-name <tenant-name> --no-browser` and deployment preflight commands.

## Database Strategy

Oyako uses portable SQLite and bootstraps the schema at application startup. The deployment scripts keep SQLite as an app-local file:

- ACA: `/app/data/<tenant_name>/oyako.sqlite`
- AWA: `/home/oyako-data/<tenant_name>/oyako.sqlite`

This satisfies the no-external-DB rule, but ACA container filesystem data is not a durable database strategy across aggressive recreation, image replacement, or instance loss. The ACA script is intentionally cutover-oriented and may reset local SQLite state when it recreates the Container App or Environment. The AWA path uses App Service `/home`, which is more stable for app-local files, but it is still not a separately managed database service.

## Secrets and Environment Files

Root `oyako.env` is committed and must remain secrets-free. It stores tenant-agnostic defaults such as:

- `default_tenant_id`
- `default_tenant_name`
- `azure_subscription`
- `azure_location`
- `awa_sku`
- `awa_runtime`
- `awa_linux_fx_version`
- `awa_scm_settle_seconds`
- GitHub Actions setup/runtime readiness settings

Both Azure deployment scripts fail fast if required cloud env files or required keys are missing:

- `azure-cloud.env`: `AzureAi__Endpoint`, `AzureAi__DeploymentName`, `AzureAi__Deployments__0`, `AzureAi__ApiVersion`, `AzureAi__ApiKey`
- `ollama-cloud.env`: `ollama_api_key`

These files are local-only and ignored by Git. The scripts do not invent secrets.

Tenant `.env` files may contain deployment names, public branding, and local SQLite paths. They are still ignored by Git because real tenant configuration can later include private operational values.

Root provider `.env.example` files are intentionally not committed. Keep required provider key names documented here and keep tenant examples centralized in `.tenants/.template.env.example`.

## GitHub Actions AWA Release

`.github/workflows/release-awa.yml` publishes the default tenant to Azure Web App when `main` receives a push. It uses:

- `actions/checkout@v5`
- `actions/setup-dotnet@v5`
- `actions/setup-node@v6`
- `azure/login@v3`

Required GitHub Secrets:

- `AZURE_CREDENTIALS`: service principal JSON for Azure login.
- `TENANT_OYAKDIJITAL_ENV`: full ignored `.tenants/oyakdijital.env` content.
- `AZURE_CLOUD_ENV`: full ignored `azure-cloud.env` content.
- `OLLAMA_CLOUD_ENV`: full ignored `ollama-cloud.env` content.

The workflow materializes these ignored files on the runner, runs `deploy-awa.cmd` without tenant arguments, and relies on `oyako.env` to resolve `oyakdijital`. After the deploy script's own smoke tests pass, the workflow passively waits for the startup crawler/refresh worker to make knowledge available by polling `/api/knowledge-health` and checking `/api/ready-questions`. It does not call `POST /api/knowledge-source-refresh`, so the release does not duplicate the startup crawl. The optional streamed Q&A probe is disabled by default and can be enabled for manual `workflow_dispatch`.

## Tenant Brand Assets

Tenant brand logo SVGs are served locally from `webapp-oyako/public/tenants/<tenant-name>/brand-logo.svg` so deployed pages do not depend on remote logo hotlinks. Verify trademark and brand usage approvals before public production rollout.

## Playwright and Browser Health

The backend includes `/health/browser` so deployments can verify that Chromium-backed scraping works.

- ACA uses the ASP.NET runtime image, installs Playwright Chromium and OS dependencies at image build time, and then runs the smallest tested CPU/memory profile that passes frontend, `/health`, and `/health/browser`.
- AWA uses Azure's built-in Linux App Service runtime. The script publishes the backend for `linux-x64`, includes the Linux Playwright driver assets, writes an LF-only `startup.sh`, installs Playwright OS dependencies when the built-in image lacks them, installs Chromium into `/home/oyako-playwright/ms-playwright`, then requires `/health/browser` to pass. If this chain fails, the script stops instead of silently producing a degraded deployment.

## NuGet Audit Mode

The backend app and test projects use `NuGetAuditMode=direct`. The current Microsoft.Data.Sqlite package graph resolves the latest available `SQLitePCLRaw.lib.e_sqlite3` package as a transitive dependency, and NuGet currently reports a transitive advisory for that package. Direct package references remain audited, while deployment/build/test output stays warning-free for the strict scripts. Revisit this when Microsoft.Data.Sqlite or SQLitePCLRaw publishes a fixed transitive package set.

## Log Analytics

`deploy-aca.cmd` creates the Container Apps Environment with `--logs-destination none` to avoid provisioning a Log Analytics Workspace. If a future Azure CLI/API version requires logs, the script should be updated explicitly and this note must be revised.
