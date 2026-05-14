project(
    'fr-monitoring', 'cpp',
    version : '0.0.4',
    license : 'MIT',
    default_options : ['cpp_std=c++26'])

include_dirs = include_directories('include')

pipewire_dep = dependency('libpipewire-0.3')
wireplumber_dep = dependency('wireplumber-0.5')

subdir('src')
