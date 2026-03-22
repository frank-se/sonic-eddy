libfrmidimapper = shared_library(
    'frmidimapper',
    sources : [
        'frmidimapper.cpp',
        'midi/Receiver.cpp',
        'midi/Sender.cpp',
        'midi/Processor.cpp',
        'midi/parse_ump.cpp',
        'midi/parse_midi.cpp',
        'midi/ActionContainer.cpp',
        'midi/ActionContainer/MidiActions.cpp',
        'midi/ActionContainer/SetVolumesBehavior.cpp',
        'midi/ActionContainer/ControlChangeParamBehavior.cpp',
        'midi/ActionContainer/NoteOnCallbackBehavior.cpp',
        'midi/ActionContainer/NoteOnLayerSelectBehavior.cpp',
        'feedback/MidiMixFeedbackManager.cpp',
        'midi/build_midi.cpp',
        'midi/build_ump.cpp',
        'audio/pan.cpp',
        'controllers/MidiMix.cpp',
        'controllers/CmdMm1.cpp',
        'controllers/Channel.cpp',
        'registry/Registry.cpp',
        'registry/Node.cpp',
        'pipewire/Pipewire.cpp',
    ],
    include_directories : include_dirs,
    version : meson.project_version(),
    soversion : '0',
    link_args : ['-latomic'],
    dependencies : [pipewire_dep, boost_dep])

conf_data = configuration_data()
conf_data.set('LIBFRMIDIMAPPER_VERSION', meson.project_version())
version_array = meson.project_version().split('.')
conf_data.set('LIBFRMIDIMAPPER_VERSION_MAJOR', version_array[0])
conf_data.set('LIBFRMIDIMAPPER_VERSION_MINOR', version_array[1])
conf_data.set('LIBFRMIDIMAPPER_VERSION_PATCH', version_array[2])

configure_file(
    output : 'libfrmidimapper_config.h',
    input : 'libfrmidimapper_config.h.in',
    configuration : conf_data)

build_inc = include_directories('.')

executable(
    'test_midi_processor',
    sources : [
        'frmidimapper.cpp',
        'midi/Receiver.cpp',
        'midi/Sender.cpp',
        'midi/Processor.cpp',
        'midi/parse_ump.cpp',
        'midi/parse_midi.cpp',
        'midi/ActionContainer.cpp',
        'midi/ActionContainer/MidiActions.cpp',
        'midi/ActionContainer/SetVolumesBehavior.cpp',
        'midi/ActionContainer/ControlChangeParamBehavior.cpp',
        'midi/ActionContainer/NoteOnCallbackBehavior.cpp',
        'midi/ActionContainer/NoteOnLayerSelectBehavior.cpp',
        'feedback/MidiMixFeedbackManager.cpp',
        'midi/build_midi.cpp',
        'midi/build_ump.cpp',
        'audio/pan.cpp',
        'controllers/MidiMix.cpp',
        'controllers/CmdMm1.cpp',
        'controllers/Channel.cpp',
        'registry/Registry.cpp',
        'registry/Node.cpp',
        'pipewire/Pipewire.cpp',
        'apps/test_midi_processor.cpp'
    ],
    link_args : ['-latomic'],
    include_directories : include_dirs,
    dependencies : [pipewire_dep, boost_dep])
