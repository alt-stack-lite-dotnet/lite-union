#!/bin/bash
# Runs the strict comparison in shards (one set of types x one number of cases at a time), in the background of a long
# session. Only the adapters that passed the self-check are timed.
#
#   ./run_strict.sh <label> [pause-seconds]      e.g.  ./run_strict.sh win-run1
#
# Results go to results/<label>/ (BenchmarkDotNet json, csv, markdown). The shards are listed in the order they run;
# a log line says which one is running, so the progress can be followed.
set -u
cd "$(dirname "$0")"

label="${1:?label of the run, e.g. win-run1}"
pause="${2:-45}"
out="results/$label"
mkdir -p "$out"

echo "[$(date +%H:%M:%S)] build"
dotnet build -c Release 2>&1 | grep -E " error |Build succeeded|Ошибок" | head -3

echo "[$(date +%H:%M:%S)] self-check"
dotnet run -c Release --no-build -- --selfcheck "$out/selfcheck.csv" | tail -2

if [ -n "${DRY:-}" ]; then echo "[dry run] stopped after the self-check"; exit 0; fi

for set in Values Refs Mixed Big; do
  for n in 3 8; do
    filters=$(dotnet run -c Release --no-build -- --filters "$out/selfcheck.csv" "*_${set}_${n}")
    count=$(echo "$filters" | wc -w)
    echo "[$(date +%H:%M:%S)] shard $set/$n: $count classes"
    # shellcheck disable=SC2086
    dotnet run -c Release --no-build -- --filter $filters --artifacts "$out" > "$out/shard-${set}-${n}.log" 2>&1
    echo "[$(date +%H:%M:%S)]   done, exit code $?"
    sleep "$pause"
  done
done

echo "[$(date +%H:%M:%S)] finished: $out"
