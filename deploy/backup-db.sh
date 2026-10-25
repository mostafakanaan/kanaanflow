#!/usr/bin/env bash
# Nightly KanaanFlow database backup (run by kanaanflow-backup.timer). Keeps the last 14 days.
# Restore: docker exec -i kanaanflow-db pg_restore -U kanaanflow -d kanaanflow --clean < <file>.dump
set -euo pipefail

dir=/var/backups/kanaanflow
mkdir -p "$dir"
chmod 700 "$dir"
file="$dir/kanaanflow-$(date +%Y-%m-%d).dump"

docker exec kanaanflow-db pg_dump -U kanaanflow -d kanaanflow --format=custom > "$file.tmp"
mv "$file.tmp" "$file"
find "$dir" -name 'kanaanflow-*.dump' -mtime +14 -delete
echo "Backup written: $file ($(du -h "$file" | cut -f1))"
