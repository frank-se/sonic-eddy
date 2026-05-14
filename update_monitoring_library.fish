#!/bin/fish
cd fr-monitoring
rm -rf release
meson setup release --buildtype release --optimization 3
cd release
meson compile
mkdir -p ../../Fr.Pw.Monitoring/runtimes/linux-x64
cp ./src/libfrmonitoring.so.0.* ../../Fr.Pw.Monitoring/runtimes/linux-x64
