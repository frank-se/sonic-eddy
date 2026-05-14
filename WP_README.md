# Update c library version

- Update library version in meson file fr-wireplumber/meson.build
- Update version of library file to include in package in Fr.Wireplumber/Fr.Wireplumber.csproj
- Update library name constant in Fr.Wireplumber/PInvoke/FrWireplumberLib.cs
- Run `update_library.fish` to build and copy the new library to the right place for packaging

# Update FrWireplumber documentation

- Go to `Fr.Wireplumber` and run `build_and_publish_documentation_container.fish`

