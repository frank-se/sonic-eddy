project(
    'pw-video-compositor', 'cpp',
    version : '0.1.0',
    license : 'MIT',
    default_options : ['cpp_std=c++20'])

pipewire_dep = dependency('libpipewire-0.3')
threads_dep = dependency('threads')

executable(
    'pw-video-compositor',
    'src/main.cpp',
    dependencies : [pipewire_dep, threads_dep],
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
