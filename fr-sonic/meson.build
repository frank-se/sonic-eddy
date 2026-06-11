project(
    'fr-sonic', 'cpp',
    version : '0.5.0',
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
threads_dep = dependency('threads')

add_project_arguments('-DLINK_PLATFORM_LINUX=1', language : 'cpp')

if get_option('buildtype') == 'release' or get_option('buildtype') == 'minsize'
  add_project_arguments('-DFRSONIC_LOG_LEVEL=2', language : 'cpp')  # Warning
else
  add_project_arguments('-DFRSONIC_LOG_LEVEL=6', language : 'cpp')  # Trace
endif

link_inc = include_directories(
    'third_party/link/include',
    'third_party/link/modules/asio-standalone/asio/include'
)

subdir('src')
