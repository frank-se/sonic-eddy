project(
    'fr-midi-mapper', 'cpp',
    version : '0.0.4',
    license : 'MIT',
    default_options : ['cpp_std=c++26'])

include_dirs = include_directories('include')

pipewire_dep = dependency('libpipewire-0.3')
boost_dep = dependency('boost')

subdir('src')
