#!/bin/fish
cd pw-midi-mapper
rm -rf release
meson setup release --buildtype release --optimization 3
cd release
meson compile
mkdir -p ../../Fr.Pw.Midi/runtimes/linux-x64
cp ./src/libfrmidimapper.so.0.* ../../Fr.Pw.Midi/runtimes/linux-x64

