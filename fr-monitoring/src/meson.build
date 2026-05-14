libfrmonitoring = shared_library(
    'frmonitoring',
    sources : [
        'frmonitoring.cpp',
        'streams/stream.cpp',
        'monitor/monitor.cpp'
    ],
    include_directories : include_dirs,
    version : meson.project_version(),
    soversion : '0',
    dependencies : [pipewire_dep, wireplumber_dep])

conf_data = configuration_data()
conf_data.set('LIBFRMONITORING_VERSION', meson.project_version())
version_array = meson.project_version().split('.')
conf_data.set('LIBFRMONITORING_VERSION_MAJOR', version_array[0])
conf_data.set('LIBFRMONITORING_VERSION_MINOR', version_array[1])
conf_data.set('LIBFRMONITORING_VERSION_PATCH', version_array[2])

configure_file(
    output : 'libfrmonitoring_config.h',
    input : 'libfrmonitoring_config.h.in',
    configuration : conf_data)

build_inc = include_directories('.')

executable(
    'test_monitor',
    sources : [
        'frmonitoring.cpp',
        'streams/stream.cpp',
        'monitor/monitor.cpp',
        'apps/test_monitor.cpp'
    ],
    include_directories : [include_dirs, build_inc],
    dependencies : [pipewire_dep, wireplumber_dep])
