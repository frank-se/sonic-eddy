using System;
using System.Threading.Tasks;
using SonicEddy.Services.AppData;

namespace SonicEddy.Services.Preferences;

public class PreferenceService(IAppDataService appDataService)
    : IPreferenceService
{
    public Contracts.ApplicationPreferences.Preferences? Preferences
    {
        get;
        private set;
    }

    public event Action? Changed;

    public async Task Load()
    {
        Preferences = await appDataService.LoadPreferences();
    }

    public async Task UpdateAndSave(
        Contracts.ApplicationPreferences.Preferences preferences)
    {
        Preferences = preferences;
        await appDataService.StorePreferences(Preferences);
        Changed?.Invoke();
    }
}