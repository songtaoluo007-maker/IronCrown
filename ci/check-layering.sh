#!/usr/bin/env bash
# ci/check-layering.sh — 分层红线门禁（无需 Unity）
# 核心层(Domain/Simulation/Contracts) 须 noEngineReferences：禁 UnityEngine / JsonUtility / IronCrown.Core
# Presentation 不得 using Domain/Simulation（PROJECT_RULES 规则 4）
set -uo pipefail

fail=0
core_dirs="Assets/Scripts/Domain Assets/Scripts/Simulation Assets/Scripts/Contracts"

check() {
  local desc="$1"; local pattern="$2"; shift 2
  local hits
  hits=$(grep -rnE "$pattern" --include="*.cs" "$@" 2>/dev/null || true)
  if [ -n "$hits" ]; then
    echo "FAIL: $desc"
    echo "$hits"
    fail=1
  else
    echo "ok  : $desc"
  fi
}

echo "== 核心层依赖红线 =="
check "核心层不得 using UnityEngine" '^[[:space:]]*using[[:space:]]+UnityEngine' $core_dirs
check "核心层不得用 JsonUtility"      'JsonUtility' $core_dirs
check "核心层不得残留 IronCrown.Core" 'IronCrown\.Core' $core_dirs

echo "== Presentation 分层（规则 4）=="
check "Presentation 不得 using Domain"     '^[[:space:]]*using[[:space:]]+IronCrown\.Domain' Assets/Scripts/Presentation
check "Presentation 不得 using Simulation" '^[[:space:]]*using[[:space:]]+IronCrown\.Simulation' Assets/Scripts/Presentation

if [ "$fail" -ne 0 ]; then
  echo ""
  echo "分层红线校验失败：核心层须无 Unity 依赖、Presentation 不碰 Domain/Simulation。"
  exit 1
fi
echo ""
echo "分层红线校验通过。"
