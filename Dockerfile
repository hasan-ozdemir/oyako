# syntax=docker/dockerfile:1.7

FROM node:24-bookworm-slim AS web-build
WORKDIR /src/webapp-oyako
ENV NPM_CONFIG_AUDIT=false
ENV NPM_CONFIG_FUND=false
ENV NPM_CONFIG_UPDATE_NOTIFIER=false
COPY webapp-oyako/package*.json ./
RUN --mount=type=cache,target=/root/.npm npm ci --no-audit --no-fund --loglevel=error
COPY webapp-oyako/ ./
ENV VITE_API_BASE=/api
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS api-build
WORKDIR /src
COPY webapi-oyako/webapi-oyako.csproj webapi-oyako/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet restore webapi-oyako/webapi-oyako.csproj
COPY webapi-oyako/ webapi-oyako/
RUN --mount=type=cache,target=/root/.nuget/packages dotnet publish webapi-oyako/webapi-oyako.csproj -c Release -o /out/api /p:UseAppHost=false

FROM mcr.microsoft.com/playwright/dotnet:v1.60.0-noble AS runtime
LABEL com.oyako.app="oyako"
LABEL com.oyako.role="fullstack-production"
LABEL com.oyako.version="v2026.6.18.300"

USER root
ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="/usr/share/dotnet:${PATH}"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=
ENV ASPNETCORE_HTTPS_PORTS=
ENV OYAKO_DOCKER=1
ENV Ai__DefaultProvider=ollama-cloud
ENV Ai__DisabledProviders__0=ollama-local
ENV Storage__DataRoot=/app/data
ENV Sqlite__ConnectionString="Data Source=/app/data/oyako.sqlite;Cache=Shared"

RUN if ! dotnet --list-runtimes | grep -q 'Microsoft.AspNetCore.App 10\.'; then \
      curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
      && bash /tmp/dotnet-install.sh --install-dir /usr/share/dotnet --channel 10.0 --runtime aspnetcore \
      && bash /tmp/dotnet-install.sh --install-dir /usr/share/dotnet --channel 10.0 --runtime dotnet \
      && rm -f /tmp/dotnet-install.sh; \
    fi

WORKDIR /app
COPY --from=api-build /out/api/ /app/
COPY --from=web-build /src/webapp-oyako/dist/ /app/wwwroot/
RUN mkdir -p /app/data \
    && chown -R pwuser:pwuser /app

USER pwuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "webapi-oyako.dll"]
