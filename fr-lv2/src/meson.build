libfrlv2 = shared_library(
    'frlv2',
    sources : [
        'frlv2.cpp', 'lv2_wrapper.cpp'
    ],
    include_directories : include_dirs,
    version : meson.project_version(),
    soversion : '0',
    dependencies : [lv2_dep, lilv_dep])

conf_data = configuration_data()
conf_data.set('LIBFRLV2_VERSION', meson.project_version())
version_array = meson.project_version().split('.')
conf_data.set('LIBFRLV2_VERSION_MAJOR', version_array[0])
conf_data.set('LIBFRLV2_VERSION_MINOR', version_array[1])
conf_data.set('LIBFRLV2_VERSION_PATCH', version_array[2])

configure_file(
    output : 'libfrlv2_config.h',
    input : 'libfrlv2_config.h.in',
    configuration : conf_data)

build_inc = include_directories('.')

executable(
    'test_app',
    sources : [
        'apps/test_app.cpp',
        'frlv2.cpp', 'lv2_wrapper.cpp'
    ],
    include_directories : include_dirs,
    dependencies : [lv2_dep, lilv_dep])
