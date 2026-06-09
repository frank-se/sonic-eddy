public_inc  = include_directories('../include')
src_inc     = include_directories('.')
core_inc    = include_directories('core')
wp_inc      = include_directories('wireplumber')
mon_inc     = include_directories('monitoring')
midi_inc    = include_directories('midi')
lv2_inc     = include_directories('lv2')
sync_inc    = include_directories('sync')
looper_inc  = include_directories('looper')
silence_inc  = include_directories('silence')

all_inc = [public_inc, src_inc, core_inc, wp_inc, mon_inc, midi_inc, lv2_inc, sync_inc, silence_inc, looper_inc, link_inc]

libfrsonic = shared_library(
    'frsonic',
    sources : [
        'frsonic.cpp',
        # Core (WirePlumber + GLib event loop)
        'core/Core.cpp',
        # WirePlumber subsystem (unchanged from fr-wireplumber)
        'wireplumber/models/objects/wireplumber_object.cpp',
        'wireplumber/models/props/props.cpp',
        'wireplumber/models/prop_info/prop_info.cpp',
        'wireplumber/models/params/param_update.cpp',
        'wireplumber/spa_helpers/spa_pod_helpers.cpp',
        'wireplumber/modules/module_factory.cpp',
        'wireplumber/params/params_handling.cpp',
        'wireplumber/props/props_handling.cpp',
        'wireplumber/metadata/metadata.cpp',
        'wireplumber/metadata/metadata_collection.cpp',
        # Monitoring subsystem
        'monitoring/Monitor.cpp',
        'monitoring/Stream.cpp',
        # MIDI subsystem
        'midi/Processor.cpp',
        'midi/Receiver.cpp',
        'midi/Sender.cpp',
        'midi/MidiSyncSender.cpp',
        'midi/MidiManipulator.cpp',
        'midi/parse_midi.cpp',
        'midi/parse_ump.cpp',
        'midi/build_midi.cpp',
        'midi/build_ump.cpp',
        'midi/audio/pan.cpp',
        'midi/controllers/FaderfoxPc4.cpp',
        'midi/controllers/MidiMix.cpp',
        'midi/controllers/CmdMm1.cpp',
        'midi/controllers/LaunchpadMini.cpp',
        'midi/controllers/Channel.cpp',
        'midi/controllers/CatchUpHandler.cpp',
        'midi/Registry.cpp',
        'midi/Node.cpp',
        # LV2 subsystem
        'lv2/frlv2.cpp',
        'lv2/lv2_wrapper.cpp',
        # Sync subsystem
        'sync/SyncMaster.cpp',
        'sync/SyncClient.cpp',
        'sync/ClickSyncConverter.cpp',
        'sync/LinkSyncAdapter.cpp',
        # Silence producers
        'silence/SilenceProducer.cpp',
        # Looper subsystem
        'looper/Looper.cpp',
    ],
    include_directories : all_inc,
    version : meson.project_version(),
    soversion : '0',
    link_args : ['-latomic'],
    dependencies : [pipewire_dep, wireplumber_dep, boost_dep, lv2_dep, lilv_dep, flac_dep, threads_dep])

conf_data = configuration_data()
conf_data.set('LIBFRSONIC_VERSION', meson.project_version())
version_array = meson.project_version().split('.')
conf_data.set('LIBFRSONIC_VERSION_MAJOR', version_array[0])
conf_data.set('LIBFRSONIC_VERSION_MINOR', version_array[1])
conf_data.set('LIBFRSONIC_VERSION_PATCH', version_array[2])

configure_file(
    output : 'libfrsonic_config.h',
    input : 'libfrsonic_config.h.in',
    configuration : conf_data)

build_inc = include_directories('.')

executable(
    'frsonic-test',
    sources : [
        'apps/test_app.cpp',
    ],
    include_directories : [all_inc, build_inc],
    link_with : libfrsonic,
    dependencies : [pipewire_dep, wireplumber_dep, boost_dep, lv2_dep, lilv_dep])

executable(
    'se-looper',
    sources : [
        'apps/looper_cli.cpp',
    ],
    include_directories : [all_inc, build_inc],
    link_with : libfrsonic,
    dependencies : [pipewire_dep, wireplumber_dep, boost_dep, lv2_dep, lilv_dep])

executable(
    'se-signal',
    sources : [
        'apps/signal_app.cpp',
        'sync/SyncClient.cpp',
    ],
    include_directories : [all_inc, build_inc],
    dependencies : [pipewire_dep, wireplumber_dep, boost_dep, lv2_dep, lilv_dep])

executable(
    'se-record',
    sources : [
        'apps/record_app.cpp',
    ],
    include_directories : [all_inc, build_inc],
    dependencies : [pipewire_dep, wireplumber_dep, boost_dep, lv2_dep, lilv_dep])

executable(
    'se-simple-producer',
    sources : [
        'apps/simple_producer.cpp',
    ],
    include_directories : [all_inc, build_inc],
    dependencies : [pipewire_dep])

executable(
    'se-sync-producer',
    sources : [
        'apps/sync_producer.cpp',
        'sync/SyncClient.cpp',
    ],
    include_directories : [all_inc, build_inc],
    dependencies : [pipewire_dep])
