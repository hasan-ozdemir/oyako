# Oyako Release 2026.6.18.300

This is the public pre-alpha baseline prepared for GitHub publication and Azure Container Apps deployment.

## Release Guarantees

- Runtime secrets and generated data are excluded from source control.
- Backend tests passed locally after the stale running API process was stopped.
- Frontend build and lint passed locally.
- Azure Container Apps deploy automation uses `<tenant_name>-<tenant_order_number>:latest` and verifies ACR tag retention.
- Azure Web App deploy automation publishes one ASP.NET-hosted React SPA ZIP without Docker or ACR.
- Public documentation and agent operating instructions are included.
