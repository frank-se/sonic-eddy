# Installation

## Pre-Requisites

Sonic Eddy depends on the following C/C++ libraries that need to be installed
with the package manager of the distribution.

- `pipewire`
- `wireplumber`
- `boost`
- `lv2`
- `lilv`
- `flac`

Sonic Eddy also depends on the following LV2 plugins.

- <http://gareus.org/oss/lv2/fil4#stereo>
- <http://calf.sourceforge.net/plugins/TransientDesigner>
- <http://calf.sourceforge.net/plugins/Compressor>
- <http://calf.sourceforge.net/plugins/Saturator>
- <urn:dragonfly:room>
- <urn:dragonfly:plate>

The .NET 10 SDK is required to build and run the C# application. Install it
from <https://dotnet.microsoft.com/download> or via your distribution's package
manager.

## PipeWire file descriptor limit

PipeWire's default file descriptor limit is too low for the many nodes
Sonic Eddy creates. Raise it before starting the app:

```bash
mkdir -p ~/.config/systemd/user/pipewire.service.d
cat > ~/.config/systemd/user/pipewire.service.d/limits.conf << 'EOF'
[Service]
LimitNOFILE=65536
EOF
systemctl --user daemon-reload
systemctl --user restart pipewire
```

## Installation from source

### Checkout the source code

```bash
git clone https://git.sr.ht/~frank6/sonic-eddy
```

### Build the native library

```bash
cd sonic-eddy/fr-sonic
meson setup build --buildtype release
meson compile -C build
```

### Build and run the application

```bash
cd ..
dotnet run --project SonicEddy -c Release
```
