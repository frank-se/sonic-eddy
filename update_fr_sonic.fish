#!/bin/fish
cd fr-sonic
rm -rf release
meson setup release --buildtype release --optimization 3
cd release
meson compile
mkdir -p ../../Fr.Sonic/runtimes/linux-x64
cp ./src/libfrsonic.so.0.1.0 ../../Fr.Sonic/runtimes/linux-x64/
