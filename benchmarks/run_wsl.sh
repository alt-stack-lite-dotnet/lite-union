#!/bin/bash
# Runs a benchmark part inside WSL (Ubuntu) from Git Bash on Windows, then brings the results back and removes what it made.
#
#   benchmarks/run_wsl.sh <label> <project>     project: Benchmark.Strict (part A, .NET 10)  or  Benchmark.Net11 (part B, .NET 11)
#   e.g.  benchmarks/run_wsl.sh wsl-run1 Benchmark.Strict
#
# What it does in WSL: copies the repository to ~/lite-union-bench (without bin/obj/results), uses the .NET SDKs that are
# already in ~/.dotnet, keeps the NuGet cache inside the same folder, runs run_strict.sh / run_net11.sh, copies
# results/<label> back to the repository, and deletes ~/lite-union-bench. Nothing else on the machine is touched.
# DRY=1 stops after the build and the self-check (to try the mechanism without the hours of measuring).
set -eu
export MSYS_NO_PATHCONV=1
label="${1:?label, e.g. wsl-run1}"
project="${2:?project: Benchmark.Strict or Benchmark.Net11}"
root="$(cd "$(dirname "$0")/.." && pwd -W)"            # C:/Users/...
wslroot="/mnt/$(echo "${root:0:1}" | tr 'A-Z' 'a-z')${root:2}"
script=$([ "$project" = "Benchmark.Net11" ] && echo run_net11.sh || echo run_strict.sh)

remote="$(mktemp --suffix=.sh)"
dry="${DRY:-}"
cat > "$remote" <<REMOTE
#!/bin/bash
set -eu
export DOTNET_ROOT=\$HOME/.dotnet
export PATH=\$PATH:\$DOTNET_ROOT
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DRY="$dry"
BASE=\$HOME/lite-union-bench
rm -rf "\$BASE"; mkdir -p "\$BASE"
tar -C "$wslroot" --exclude=bin --exclude=obj --exclude=.git --exclude=.idea --exclude=results --exclude=BenchmarkDotNet.Artifacts --exclude=out -cf - . | tar -C "\$BASE" -xf -
export NUGET_PACKAGES="\$BASE/nuget"
cd "\$BASE/benchmarks/$project"
chmod +x $script
./$script "$label"
mkdir -p "$wslroot/benchmarks/$project/results"
rm -rf "$wslroot/benchmarks/$project/results/$label"
cp -r "results/$label" "$wslroot/benchmarks/$project/results/$label"
cd "\$HOME"
rm -rf "\$BASE"
echo "results copied to benchmarks/$project/results/$label, \$BASE removed"
REMOTE
sed -i 's/\r$//' "$remote"
winpath="$(cygpath -m "$remote")"                                      # C:/Users/.../Temp/tmp.xxx.sh
wslpath="/mnt/$(echo "${winpath:0:1}" | tr 'A-Z' 'a-z')${winpath:2}"
wsl.exe -d Ubuntu -- bash "$wslpath" 2>&1 | tr -d '\0'
rm -f "$remote"
