#!/bin/fish
cd fr-wireplumber
rm -rf release
meson setup release --buildtype release --optimization 3
cd release
meson compile
mkdir -p ../../Fr.Wireplumber/runtimes/linux-x64
cp ./src/libfrwireplumber.so.0.* ../../Fr.Wireplumber/runtimes/linux-x64
