#!/usr/bin/env bash
set -euo pipefail

OUTPUT_DIR="${1:-deploy/keys}"
PRIVATE_KEY_NAME="${2:-update-private.pem}"
PUBLIC_KEY_NAME="${3:-update-public.pem}"

mkdir -p "${OUTPUT_DIR}"

PRIVATE_PATH="${OUTPUT_DIR}/${PRIVATE_KEY_NAME}"
PUBLIC_PATH="${OUTPUT_DIR}/${PUBLIC_KEY_NAME}"

openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 -out "${PRIVATE_PATH}"
openssl rsa -pubout -in "${PRIVATE_PATH}" -out "${PUBLIC_PATH}"

echo "Ключи подписи обновлений созданы:"
echo "  Private: ${PRIVATE_PATH}"
echo "  Public : ${PUBLIC_PATH}"
