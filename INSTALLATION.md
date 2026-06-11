# Installation

## Pre-Requisites

Sonic Eddy depends on the following c/c++ libraries that need to be installed
with the package manager of the distribution.

- `pipewire`
- `wireplumber`
- `boost`
- `lv2`
- `lilv`
- `flac`
- `threads`

Sonic Eddy also depends on the following LV2 plugins.

- <http://gareus.org/oss/lv2/fil4#stereo>
- <http://calf.sourceforge.net/plugins/TransientDesigner>
- <http://calf.sourceforge.net/plugins/Compressor>
- <http://calf.sourceforge.net/plugins/Saturator>
- <urn:dragonfly:room>
- <urn:dragonfly:plate>

## Installation

### Checkout the source code

```bash
git clone git clone --recurse-submodules https://git.sr.ht/~frank6/sonic-eddy
```

### Build the c library

```bash
cd sonic-eddy/fr-sonic
meson setup build --buildtype release
```
