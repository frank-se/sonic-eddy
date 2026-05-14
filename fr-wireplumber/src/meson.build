libfrwireplumber = shared_library(
    'frwireplumber',
    sources : [
        'frwireplumber.cpp',
        'processing/WireplumberThread.cpp',
        'models/objects/wireplumber_object.cpp',
        'models/props/props.cpp',
        'models/prop_info/prop_info.cpp',
        'models/params/param_update.cpp',
        'spa_helpers/spa_pod_helpers.cpp',
        'modules/module_factory.cpp',
        'params/params_handling.cpp',
        'props/props_handling.cpp',
        'metadata/metadata.cpp',
        'metadata/metadata_collection.cpp'
    ],
    include_directories : include_dirs,
    version : meson.project_version(),
    soversion : '0',
    dependencies : [pipewire_dep, wireplumber_dep])

conf_data = configuration_data()
conf_data.set('LIBFRWIREPLUMBER_VERSION', meson.project_version())
version_array = meson.project_version().split('.')
conf_data.set('LIBFRWIREPLUMBER_VERSION_MAJOR', version_array[0])
conf_data.set('LIBFRWIREPLUMBER_VERSION_MINOR', version_array[1])
conf_data.set('LIBFRWIREPLUMBER_VERSION_PATCH', version_array[2])

configure_file(
    output : 'libfrwireplumber_config.h',
    input : 'libfrwireplumber_config.h.in',
    configuration : conf_data)

build_inc = include_directories('.')

executable(
    'test-listener',
    sources : [
        'apps/test_listener.cpp',
        'frwireplumber.cpp',
        'processing/WireplumberThread.cpp',
        'models/objects/wireplumber_object.cpp',
        'models/props/props.cpp',
        'models/prop_info/prop_info.cpp',
        'models/params/param_update.cpp',
        'spa_helpers/spa_pod_helpers.cpp',
        'modules/module_factory.cpp',
        'params/params_handling.cpp',
        'props/props_handling.cpp',
        'metadata/metadata.cpp',
        'metadata/metadata_collection.cpp'
    ],
    include_directories : [include_dirs, build_inc],
    dependencies : [pipewire_dep, wireplumber_dep])