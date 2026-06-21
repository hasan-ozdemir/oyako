# Tenant Environment Files

Real tenant files in this directory use the pattern `<tenant-name>.env` and are git ignored.
Commit only `.env.example` files.

Default tenant selection is `oyakdijital` when no `--tenant-name`, `-t`, or `OYAKO_TENANT_NAME` value is supplied.

Provider secrets stay in `azure-cloud.env` and `ollama-cloud.env`; tenant files hold only safe tenant identity, public UI, model, and deployment naming values.

Each tenant file must declare at least one seed knowledge source:

- `tenant_knowledge_source_1_type=web_site`
- `tenant_knowledge_source_1_url=https://...`
- `tenant_knowledge_source_1_refresh_period=1hour`

Refresh periods accept `1..60minute`, `1..24hour`, `1..4day`, or `1..4week` values, with optional plural suffixes. Seed sources are tenant-owned baseline sources; admin-created sources and documents are preserved separately.

Optional source display fields are supported with the same index:

- `tenant_knowledge_source_1_name`
- `tenant_knowledge_source_1_description`
- `tenant_knowledge_source_1_enabled`

Tenant brand SVGs are served from `webapp-oyako/public/tenants/<tenant-name>/brand-logo.svg`; use local paths in `ui_web_brand_logo_url` so deployed pages do not depend on remote logo hotlinks.
