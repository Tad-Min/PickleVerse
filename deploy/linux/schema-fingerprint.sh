#!/usr/bin/env bash
set -euo pipefail

# pg_dump 16 adds comments and per-run psql restrict keys to the same schema.
LC_ALL=C sed -E '/^--/d; /^\\(un)?restrict[[:space:]]/d; /^[[:space:]]*$/d' |
  sha256sum | cut -d' ' -f1
