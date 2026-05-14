#!/bin/fish
cd fr-lv2
rm -rf release
meson setup release --buildtype release --optimization 3
cd release
meson compile
mkdir -p ../../Fr.Lv2/runtimes/linux-x64
cp ./src/libfrlv2.so.0.* ../../Fr.Lv2/runtimes/linux-x64
