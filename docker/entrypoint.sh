#!/usr/bin/env bash
set -Eeuo pipefail

API_PID=""
NGINX_PID=""
REFRESH_PID=""

cleanup() {
  echo "[entrypoint] Stopping Oyako services..."
  if [[ -n "${NGINX_PID}" ]]; then
    kill -TERM "${NGINX_PID}" 2>/dev/null || true
  fi
  if [[ -n "${REFRESH_PID}" ]]; then
    kill -TERM "${REFRESH_PID}" 2>/dev/null || true
  fi
  if [[ -n "${API_PID}" ]]; then
    kill -TERM "${API_PID}" 2>/dev/null || true
  fi
  wait 2>/dev/null || true
}

wait_for_http() {
  local url="$1"
  local name="$2"
  local seconds="$3"
  local deadline=$((SECONDS + seconds))
  until curl -fsS "${url}" >/tmp/oyako-health.json 2>/tmp/oyako-health.err; do
    if (( SECONDS >= deadline )); then
      echo "[entrypoint] Timed out waiting for ${name}: ${url}"
      cat /tmp/oyako-health.err 2>/dev/null || true
      return 1
    fi
    sleep 1
  done
}

trap cleanup EXIT INT TERM

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
unset ASPNETCORE_URLS ASPNETCORE_HTTP_PORTS ASPNETCORE_HTTPS_PORTS HTTP_PORTS HTTPS_PORTS
export OYAKO_DOCKER="${OYAKO_DOCKER:-1}"
export Ai__DefaultProvider="${Ai__DefaultProvider:-ollama-cloud}"
export Ai__DisabledProviders__0="${Ai__DisabledProviders__0:-ollama-local}"

echo "[entrypoint] Starting Oyako Web API on http://0.0.0.0:5000"
cd /app/api
dotnet ./webapi-oyako.dll &
API_PID=$!

wait_for_http "http://127.0.0.1:5000/api/api-health" "Web API" 120
echo "[entrypoint] Web API is reachable."

echo "[entrypoint] Starting Oyako Web UI on http://0.0.0.0:3000"
nginx -g "daemon off;" &
NGINX_PID=$!

wait_for_http "http://127.0.0.1:3000" "Web UI" 30
echo "[entrypoint] Web UI is reachable."

(
  echo "[entrypoint] Refreshing knowledge sources for the Docker runtime in the background..."
  if curl -fsS -X POST "http://127.0.0.1:5000/api/knowledge-source-refresh" >/tmp/oyako-refresh.json 2>/tmp/oyako-refresh.err; then
    echo "[entrypoint] Knowledge source refresh completed."
  else
    echo "[entrypoint] Knowledge source refresh did not complete successfully; continuing with API-reported health."
    cat /tmp/oyako-refresh.err 2>/dev/null || true
  fi

  echo "[entrypoint] Waiting for knowledge health to report usable source/document state..."
  deadline=$((SECONDS + 240))
  while true; do
    if curl -fsS "http://127.0.0.1:5000/api/knowledge-health" >/tmp/oyako-knowledge.json 2>/tmp/oyako-knowledge.err; then
      if grep -Eq '"sourceCount":[1-9][0-9]*' /tmp/oyako-knowledge.json && grep -Eq '"pageCount":[1-9][0-9]*' /tmp/oyako-knowledge.json; then
        echo "[entrypoint] Knowledge health reports available sources and documents."
        break
      fi
    fi
    if (( SECONDS >= deadline )); then
      echo "[entrypoint] Knowledge health is still warming up; Web UI remains available with current backend status."
      cat /tmp/oyako-knowledge.json 2>/dev/null || true
      break
    fi
    sleep 2
  done
) &
REFRESH_PID=$!

wait -n "${API_PID}" "${NGINX_PID}"
EXIT_CODE=$?
echo "[entrypoint] A service exited with code ${EXIT_CODE}."
exit "${EXIT_CODE}"
