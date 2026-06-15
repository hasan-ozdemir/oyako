# syntax=docker/dockerfile:1

FROM node:24-bookworm-slim AS web-build
WORKDIR /src/webapp-oyako
ENV NPM_CONFIG_AUDIT=false
ENV NPM_CONFIG_FUND=false
ENV NPM_CONFIG_UPDATE_NOTIFIER=false
COPY webapp-oyako/package*.json ./
RUN npm ci --no-audit --no-fund --loglevel=error
COPY webapp-oyako/ ./
ENV VITE_API_BASE=/api
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS api-build
WORKDIR /src
COPY webapi-oyako/webapi-oyako.csproj webapi-oyako/
RUN dotnet restore webapi-oyako/webapi-oyako.csproj
COPY webapi-oyako/ webapi-oyako/
RUN dotnet publish webapi-oyako/webapi-oyako.csproj -c Release -o /out/api /p:UseAppHost=false

FROM mcr.microsoft.com/playwright/dotnet:v1.60.0-noble AS runtime
LABEL com.oyako.app="oyako"
LABEL com.oyako.role="fullstack-docker-dev"
LABEL com.oyako.version="v2026.6.18.300"

USER root
ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="/usr/share/dotnet:${PATH}"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV OYAKO_DOCKER=1
ENV Ai__DefaultProvider=ollama-cloud
ENV Ai__DisabledProviders__0=ollama-local

RUN apt-get update \
    && apt-get install -y --no-install-recommends nginx curl bash procps \
    && rm -rf /var/lib/apt/lists/*

RUN if ! dotnet --list-runtimes | grep -q 'Microsoft.AspNetCore.App 10\.'; then \
      curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
      && bash /tmp/dotnet-install.sh --install-dir /usr/share/dotnet --channel 10.0 --runtime aspnetcore \
      && bash /tmp/dotnet-install.sh --install-dir /usr/share/dotnet --channel 10.0 --runtime dotnet \
      && rm -f /tmp/dotnet-install.sh; \
    fi

WORKDIR /app
COPY --from=api-build /out/api /app/api
COPY --from=web-build /src/webapp-oyako/dist /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/nginx.conf
COPY docker/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

EXPOSE 3000 5000
ENTRYPOINT ["/app/entrypoint.sh"]

