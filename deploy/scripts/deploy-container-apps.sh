#!/usr/bin/env bash

set -euo pipefail

show_usage() {
  cat <<'EOF'
Usage: deploy-container-apps.sh --tag <image-tag> [options]

Required:
  --tag                Container image tag to deploy (e.g. develop-a13130d)

Options:
  --resource-group     Azure resource group (default: rg-whatismyfridge-test)
  --backend-app        Container Apps backend name (default: ca-backend-test)
  --frontend-app       Container Apps frontend name (default: ca-frontend-test)
  --registry-prefix    Image prefix without tag (default: ghcr.io/lenny32/whatisinmyfridge)
  --skip-frontend      Update backend only
  --help               Show this message

Environment:
  AZURE_CORE_OUTPUT can be set to control CLI output format. Azure login must be completed before running.
EOF
}

RESOURCE_GROUP="rg-whatismyfridge-test"
BACKEND_APP="ca-backend-test"
FRONTEND_APP="ca-frontend-test"
REGISTRY_PREFIX="ghcr.io/lenny32/whatisinmyfridge"
UPDATE_FRONTEND=true
IMAGE_TAG=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)
      IMAGE_TAG="${2:-}"
      shift 2
      ;;
    --resource-group)
      RESOURCE_GROUP="${2:-}"
      shift 2
      ;;
    --backend-app)
      BACKEND_APP="${2:-}"
      shift 2
      ;;
    --frontend-app)
      FRONTEND_APP="${2:-}"
      shift 2
      ;;
    --registry-prefix)
      REGISTRY_PREFIX="${2:-}"
      shift 2
      ;;
    --skip-frontend)
      UPDATE_FRONTEND=false
      shift
      ;;
    --help|-h)
      show_usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      show_usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "$IMAGE_TAG" ]]; then
  echo "Error: --tag is required." >&2
  show_usage >&2
  exit 1
fi

command -v az >/dev/null 2>&1 || {
  echo "Azure CLI is required but not found in PATH." >&2
  exit 1
}

REVISION_SUFFIX="$(date +%s)-${IMAGE_TAG}"
BACKEND_IMAGE="${REGISTRY_PREFIX}/backend:${IMAGE_TAG}"
FRONTEND_IMAGE="${REGISTRY_PREFIX}/frontend:${IMAGE_TAG}"

echo "Updating backend container app ${BACKEND_APP} with image ${BACKEND_IMAGE}"

FRONTEND_FQDN="$(az containerapp show \
  --name "${FRONTEND_APP}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query properties.configuration.ingress.fqdn \
  --output tsv 2>/dev/null || true)"

ALLOWED_ORIGINS=""
if [[ -n "$FRONTEND_FQDN" ]]; then
  ALLOWED_ORIGINS="https://${FRONTEND_FQDN}"
fi

az containerapp update \
  --name "${BACKEND_APP}" \
  --resource-group "${RESOURCE_GROUP}" \
  --image "${BACKEND_IMAGE}" \
  --revision-suffix "${REVISION_SUFFIX}" \
  --set-env-vars "ALLOWED_ORIGINS=${ALLOWED_ORIGINS}" \
  >/dev/null

BACKEND_FQDN="$(az containerapp show \
  --name "${BACKEND_APP}" \
  --resource-group "${RESOURCE_GROUP}" \
  --query properties.configuration.ingress.fqdn \
  --output tsv)"

echo "Backend revision updated. Current FQDN: https://${BACKEND_FQDN}"

if [[ "${UPDATE_FRONTEND}" == true ]]; then
  echo "Updating frontend container app ${FRONTEND_APP} with image ${FRONTEND_IMAGE}"
  az containerapp update \
    --name "${FRONTEND_APP}" \
    --resource-group "${RESOURCE_GROUP}" \
    --image "${FRONTEND_IMAGE}" \
    --revision-suffix "${REVISION_SUFFIX}" \
    --set-env-vars "VITE_API_BASE=https://${BACKEND_FQDN}" \
    >/dev/null

  FRONTEND_FQDN="$(az containerapp show \
    --name "${FRONTEND_APP}" \
    --resource-group "${RESOURCE_GROUP}" \
    --query properties.configuration.ingress.fqdn \
    --output tsv)"

  echo "Frontend revision updated. Current FQDN: https://${FRONTEND_FQDN}"
else
  echo "Frontend update skipped."
fi

echo "Deployment complete."
