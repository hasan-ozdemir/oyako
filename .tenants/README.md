# Tenant Environment Files

Real tenant files in this directory use the pattern `<tenant-name>.env` and are git ignored.
Commit only `.env.example` files.

Default tenant selection is `oyakdijital` when no `--tenant-name`, `-t`, or `OYAKO_TENANT_NAME` value is supplied.

Provider secrets stay in `azure-cloud.env` and `ollama-cloud.env`; tenant files hold only safe tenant identity, public UI, model, and deployment naming values.
