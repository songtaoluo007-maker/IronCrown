#!/usr/bin/env bash
# ci/check-utf8.sh — UTF-8 编码守卫
# 所有跟踪的 .md/.cs/.json 必须是合法 UTF-8（CHANGELOG 曾两次被写成乱码）
set -uo pipefail

fail=0
count=0
while IFS= read -r f; do
  [ -f "$f" ] || continue
  count=$((count + 1))
  if ! iconv -f UTF-8 -t UTF-8 "$f" >/dev/null 2>&1; then
    echo "FAIL 非法 UTF-8: $f"
    fail=1
  fi
done < <(git ls-files '*.md' '*.cs' '*.json')

echo "已检查 $count 个 .md/.cs/.json 文件。"
if [ "$fail" -ne 0 ]; then
  echo "UTF-8 守卫失败：上列文件含非法 UTF-8。"
  exit 1
fi
echo "UTF-8 守卫通过。"
