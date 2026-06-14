# Sonic Eddy

A performance focused mixer

## Setup

### Install Dependencies

TBD

### Setup

Pipewire creates memory mapped files for the exchange of data between pipewire
modules. Sonic Eddy creates a large amount of modules and, depending on existing
system settings, the limit might have to be increased.

To increase the limit add the following file to override the limit and restart
pipewire: `$HOME/.config/systemd/user/pipewire.service.d/limits.conf`.

```ini
[Service]
LimitNOFILE=65536
```

### Troubleshooting

If sound playback doesn't start, it might be due to a locked up kernel module.
Loading the module again may help, example:

```bash
sudo modprobe -r snd_hdspm && sudo modprobe snd_hdspm
```
