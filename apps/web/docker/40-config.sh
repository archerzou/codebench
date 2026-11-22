#!/bin/sh
# Writes the web app's runtime config at container start (nginx's entrypoint runs /docker-entrypoint.d/*.sh).
# One image runs in every environment: config.ts reads window.__CONFIG__ before the bundle starts.
#
# PUBLIC VALUES ONLY (FE-05): everything here is readable by every visitor. Allow-listed names below; never add
# anything that holds a server key, webhook signing value or credential.
set -eu

target=/usr/share/nginx/html/config.js

# JSON-escape a value (backslash and double quote) so a stray character can't break out of the string.
esc() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

case "${CLERK_PUBLISHABLE_KEY:-}" in
  "" | pk_*) ;;
  *) echo "40-config.sh: CLERK_PUBLISHABLE_KEY must be a publishable key (pk_...)" >&2; exit 1 ;;
esac

cat > "$target" <<EOF
window.__CONFIG__ = {
  API_URL: "$(esc "${API_URL:-}")",
  CLERK_PUBLISHABLE_KEY: "$(esc "${CLERK_PUBLISHABLE_KEY:-}")",
  MARKETING_URL: "$(esc "${MARKETING_URL:-}")"
};
EOF

echo "40-config.sh: wrote $target (API_URL=${API_URL:-<unset>}, MARKETING_URL=${MARKETING_URL:-<unset>})"
