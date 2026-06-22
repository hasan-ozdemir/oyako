#!/usr/bin/env bash
set -euo pipefail

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:${PORT:-8080}}"
export PLAYWRIGHT_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-/home/oyako-playwright/ms-playwright}"

mkdir -p /home/oyako-data "${PLAYWRIGHT_BROWSERS_PATH}"
PLAYWRIGHT_DEPS_MARKER="${PLAYWRIGHT_BROWSERS_PATH}/.oyako-deps-installed"

if [ ! -f ./webapi-oyako.dll ]; then
  echo "[startup] ERROR: webapi-oyako.dll is missing from the deployed package."
  exit 90
fi

if [ ! -f ./Microsoft.Playwright.dll ]; then
  echo "[startup] ERROR: Microsoft.Playwright.dll is missing from the deployed package."
  exit 91
fi

if [ ! -f ./.playwright/node/linux-x64/node ]; then
  echo "[startup] ERROR: Playwright linux-x64 node driver is missing from the deployed package."
  exit 92
fi

chmod +x ./.playwright/node/linux-x64/node

if [ ! -f "${PLAYWRIGHT_DEPS_MARKER}" ] && [ ! -e /usr/lib/x86_64-linux-gnu/libnspr4.so ] && [ ! -e /lib/x86_64-linux-gnu/libnspr4.so ]; then
  dotnet ./webapi-oyako.dll --install-playwright-deps
  touch "${PLAYWRIGHT_DEPS_MARKER}"
fi

if ! compgen -G "${PLAYWRIGHT_BROWSERS_PATH}/chromium-*/chrome-linux/chrome" > /dev/null; then
  dotnet ./webapi-oyako.dll --install-playwright
fi

exec dotnet ./webapi-oyako.dll
