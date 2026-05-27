project(
    'fr-sonic', 'cpp',
    version : '0.4.0',
    license : 'MIT',
    default_options : ['cpp_std=c++26'])

add_project_arguments(
    '-include',
    meson.project_source_root() / 'src/wireplumber/compiler/concepts.h',
    language : 'cpp')

pipewire_dep = dependency('libpipewire-0.3')
wireplumber_dep = dependency('wireplumber-0.5')
boost_dep = dependency('boost')
lv2_dep = dependency('lv2')
lilv_dep = dependency('lilv-0')
flac_dep = dependency('flac')

subdir('src')
