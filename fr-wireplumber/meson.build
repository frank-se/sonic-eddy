project(
    'fr-wireplumber', 'cpp',
    version : '0.9.1',
    license : 'MIT',
    default_options : ['cpp_std=c++26'])

add_project_arguments(
    '-include',
    meson.project_source_root() / 'include/compiler/concepts.h',
    language : 'cpp')

include_dirs = include_directories('include')

pipewire_dep = dependency('libpipewire-0.3')
wireplumber_dep = dependency('wireplumber-0.5')

subdir('src')
