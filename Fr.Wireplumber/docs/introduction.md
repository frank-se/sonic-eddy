# Introduction

Fr.Wireplumber provides a C# API to interact with wireplumber
and pipewire. It is focused on providing the ability to manage
pipewire nodes, for example by setting the volume, or muting
the output, and creation, and management of modules.
As such, the following functionality is considered in scope:
- Observe pipewire objects, like nodes, devices, or clients
- Create modules, and destroy them when not required anymore
- Set the volume, or mute a node
- Create links

## Limitations

It does not provide the ability to implement filters or streams
in C#.