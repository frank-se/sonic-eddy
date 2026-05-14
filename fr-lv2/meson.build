project(
    'fr-lv2', 'cpp',
    version: '0.0.3',
    license: 'MIT',
    default_options : ['cpp_std=c++26'])

include_dirs = include_directories('include')

lv2_dep = dependency('lv2')
lilv_dep = dependency('lilv-0')

subdir('src')
