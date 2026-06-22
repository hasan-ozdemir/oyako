#!/usr/bin/env bash
set -euo pipefail

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:${PORT:-8080}}"
export PLAYWRIGHT_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-/home/oyako-playwright/ms-playwright}"
export PLAYWRIGHT_DRIVER_SEARCH_PATH="${PLAYWRIGHT_DRIVER_SEARCH_PATH:-$(pwd)}"
export DEBIAN_FRONTEND=noninteractive

mkdir -p /home/oyako-data "${PLAYWRIGHT_BROWSERS_PATH}"
PLAYWRIGHT_DEPS_LOG="${PLAYWRIGHT_BROWSERS_PATH}/install-deps.log"
PLAYWRIGHT_INSTALL_LOG="${PLAYWRIGHT_BROWSERS_PATH}/install-browser.log"
PLAYWRIGHT_VERIFY_LOG="${PLAYWRIGHT_BROWSERS_PATH}/verify-browser.log"

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

if [ ! -e /usr/lib/x86_64-linux-gnu/libnspr4.so ] || [ ! -e /usr/lib/x86_64-linux-gnu/libnss3.so ] || [ ! -e /usr/lib/x86_64-linux-gnu/libgbm.so.1 ]; then
  if ! dotnet ./webapi-oyako.dll --install-playwright-deps > "${PLAYWRIGHT_DEPS_LOG}" 2>&1; then
    cat "${PLAYWRIGHT_DEPS_LOG}"
    exit 93
  fi
fi

if ! compgen -G "${PLAYWRIGHT_BROWSERS_PATH}/chromium-*/chrome-linux64/chrome" > /dev/null; then
  if ! dotnet ./webapi-oyako.dll --install-playwright > "${PLAYWRIGHT_INSTALL_LOG}" 2>&1; then
    cat "${PLAYWRIGHT_INSTALL_LOG}"
    exit 94
  fi
fi

if ! dotnet ./webapi-oyako.dll --verify-playwright > "${PLAYWRIGHT_VERIFY_LOG}" 2>&1; then
  cat "${PLAYWRIGHT_VERIFY_LOG}"
  exit 95
fi

exec dotnet ./webapi-oyako.dll
