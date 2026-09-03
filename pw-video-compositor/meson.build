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
raylib_dep = dependency('raylib')

executable(
    'pw-video-compositor',
    ['src/main.cpp', 'src/scene.cpp', 'src/camera_config.cpp'],
    include_directories : include_directories('third_party'),
    dependencies : [pipewire_dep, threads_dep, nlohmann_json_dep],
    install : true)

# Purpose-built compositor for the DSK/"downstream effects" node - kept as
# its own binary rather than a pw-video-compositor mode, since the two
# concepts (generic N-camera compositor vs. one-baseline-plus-overlays)
# were a real source of confusion when conflated. See downstream_main.cpp's
# header comment.
executable(
    'downstream-compositor',
    ['src/downstream_main.cpp', 'src/downstream_scene.cpp', 'src/downstream_camera_config.cpp'],
    include_directories : include_directories('third_party'),
    dependencies : [pipewire_dep, threads_dep, nlohmann_json_dep],
    install : true)

# Two-input, one-output linear cross-dissolve node - blends two
# pw-video-compositor instances' outputs based on a "blend_position" Props
# value, for the T-bar M/E switcher prototype. Also owns an optional
# --preview monitor window (program+preview stacked), hence the GStreamer
# deps.
executable(
    'video-blender',
    'src/video_blender.cpp',
    dependencies : [pipewire_dep, threads_dep, gst_dep, gst_app_dep],
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

# MIDI note-on/off events visualized as a 3D "cube" (X=time, Y=note number,
# Z=channel) and pushed out as its own PipeWire video source - see
# src/midi_cube_main.cpp header comment. Self-contained: its own minimal
# MIDI byte parser (no fr-sonic dependency) and raylib for offscreen 3D
# rendering with real depth-buffer occlusion and lighting.
executable(
    'midi-cube',
    ['src/midi_cube_main.cpp', 'src/cube_renderer.cpp', 'src/midi_cube_midi.cpp',
     'src/midi_note_registry.cpp'],
    include_directories : include_directories('third_party'),
    dependencies : [pipewire_dep, threads_dep, raylib_dep],
    install : true)
