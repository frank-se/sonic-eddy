project(
    'pw-video-compositor', 'cpp',
    version : '0.1.0',
    license : 'MIT',
    default_options : ['cpp_std=c++20'])

pipewire_dep = dependency('libpipewire-0.3')
threads_dep = dependency('threads')
nlohmann_json_dep = dependency('nlohmann_json')
gst_dep = dependency('gstreamer-1.0')
gst_app_dep = dependency('gstreamer-app-1.0')

executable(
    'pw-video-compositor',
    ['src/main.cpp', 'src/scene.cpp'],
    include_directories : include_directories('third_party'),
    dependencies : [pipewire_dep, threads_dep, nlohmann_json_dep],
    install : true)

# Throwaway pure-PipeWire test tools (no GStreamer) used to verify the
# compositor without depending on an external video producer/consumer setup.
executable(
    'gradient_producer',
    'test/gradient_producer.cpp',
    dependencies : [pipewire_dep, threads_dep],
    install : false)

executable(
    'dump_consumer',
    'test/dump_consumer.cpp',
    dependencies : [pipewire_dep, threads_dep],
    install : false)

# Prototype: pulls compositor video in lockstep with a real audio clock and
# hands both to an in-process GStreamer pipeline via appsrc, recording a
# synced-A/V MP4. See pw-video-compositor test/av_sync_record.cpp for why.
executable(
    'av_sync_record',
    'test/av_sync_record.cpp',
    dependencies : [pipewire_dep, threads_dep, gst_dep, gst_app_dep, nlohmann_json_dep],
    install : false)
