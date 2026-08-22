using Avalonia.Controls;

namespace SonicEddy.Views.MixerOverviewViews;

// Identical copy of MixerOverviewWindow, used only by MixerOverviewStreamService
// for its off-screen render target. Deliberately does NOT call
// WaylandAppId.Apply - MixerOverviewWindow's "sonic-eddy-overview" app_id is
// used by window-manager rules (e.g. forced sizing) that must only apply to
// the real, user-visible window, never to this hidden one.
public partial class MixerOverviewStreamWindow : Window
{
    public MixerOverviewStreamWindow()
    {
        InitializeComponent();
    }
}
