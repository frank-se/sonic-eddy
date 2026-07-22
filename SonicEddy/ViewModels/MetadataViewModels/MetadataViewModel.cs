using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DynamicData;
using Fr.Sonic.Model.Metadata;
using Fr.Sonic.Registries.Metadata;
using ReactiveUI;
using SonicEddy.Services.AppData;
using SonicEddy.Tools;
using SonicEddy.Views.MetadataViews;

namespace SonicEddy.ViewModels.MetadataViewModels;

public class MetadataViewModel : ViewModelBase
{
    public ObservableCollection<MetadataCollection>
        MetadataCollections { get; } = [];

    public ObservableCollection<MetadataEntry> MetadataEntries { get; } = [];

    public MetadataViewModel(IAppDataService appDataService)
    {
        _appDataService = appDataService;

        var defaultMetadata =
            _metadataRegistry.GetByName(_selectedMetadataName);

        if (defaultMetadata is not null)
        {
            MetadataEntries.AddRange(defaultMetadata.MetadataEntries);
            defaultMetadata.Added += HandleMetadataEntryAddedEvent;
            defaultMetadata.Updated += HandleMetadataEntryUpdatedEvent;
            defaultMetadata.Deleted += HandleMetadataEntryDeletedEvent;
        }

        MetadataCollections.AddRange(_metadataRegistry.MetadataCollections);
    }

    private readonly MetadataRegistry _metadataRegistry =
        Fr.Sonic.FrSonic.MetadataRegistry;

    private string _selectedMetadataName = "default";

    public void ChangeSelectedMetadata(object name)
    {
        var metadataName = (string)name;
        if (metadataName == _selectedMetadataName) return;

        var previous = _metadataRegistry.GetByName(_selectedMetadataName);
        if (previous is not null)
        {
            previous.Added -= HandleMetadataEntryAddedEvent;
            previous.Updated -= HandleMetadataEntryUpdatedEvent;
            previous.Deleted -= HandleMetadataEntryDeletedEvent;
        }

        _selectedMetadataName = metadataName;
        MetadataEntries.Clear();
        var metadata = _metadataRegistry.GetByName(_selectedMetadataName);

        if (metadata is null) return;
        MetadataEntries.AddRange(metadata.MetadataEntries);
        metadata.Added += HandleMetadataEntryAddedEvent;
        metadata.Updated += HandleMetadataEntryUpdatedEvent;
        metadata.Deleted += HandleMetadataEntryDeletedEvent;
    }

    private void HandleMetadataEntryAddedEvent(MetadataEntry metadataEntry)
    {
        if (_selectedMetadataName != metadataEntry.MetadataName) return;

        Dispatcher.UIThread.Post(() => { MetadataEntries.Add(metadataEntry); });
    }

    private void HandleMetadataEntryUpdatedEvent(MetadataEntry metadataEntry)
    {
        if (_selectedMetadataName != metadataEntry.MetadataName) return;

        var existing = MetadataEntries.FirstOrDefault(m =>
            m.Subject == metadataEntry.Subject &&
            m.Key == metadataEntry.Key);

        Dispatcher.UIThread.Post(() =>
        {
            if (existing is not null)
            {
                MetadataEntries.Remove(existing);
            }

            MetadataEntries.Add(metadataEntry);
        });
    }

    private void HandleMetadataEntryDeletedEvent(MetadataEntry metadataEntry)
    {
        if (_selectedMetadataName != metadataEntry.MetadataName) return;

        var existing = MetadataEntries.FirstOrDefault(m =>
            m.Subject == metadataEntry.Subject &&
            m.Key == metadataEntry.Key);

        Dispatcher.UIThread.Post(() =>
        {
            if (existing is not null)
            {
                MetadataEntries.Remove(existing);
            }
        });
    }

    public async Task AddMetadataEntry()
    {
        var dialogViewModel = new AddOrUpdateMetadataItemDialogViewModel()
        {
            IsAddMode = true
        };

        var dialog = new AddOrUpdateMetadataItemDialogView()
        {
            DataContext = dialogViewModel,
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel.DialogResult)
        {
            var metadata = _metadataRegistry.GetByName(_selectedMetadataName);
            metadata?.AddOrUpdateMetadataEntry(
                dialogViewModel.Subject,
                dialogViewModel.Key,
                dialogViewModel.Type,
                dialogViewModel.Value);
        }
    }

    public async Task UpdateMetadataEntry(object entry)
    {
        var metadataEntry = (MetadataEntry)entry;
        var dialogViewModel = new AddOrUpdateMetadataItemDialogViewModel()
        {
            IsAddMode = false,
            Key = metadataEntry.Key,
            Subject = metadataEntry.Subject,
            Type = metadataEntry.Type,
            Value = metadataEntry.Value
        };

        var dialog = new AddOrUpdateMetadataItemDialogView()
        {
            DataContext = dialogViewModel,
        };

        await dialog.ShowDialog(WindowTools.GetMainWindow()!);

        if (dialogViewModel.DialogResult)
        {
            var metadata = _metadataRegistry.GetByName(_selectedMetadataName);
            metadata?.AddOrUpdateMetadataEntry(
                dialogViewModel.Subject,
                dialogViewModel.Key,
                dialogViewModel.Type,
                dialogViewModel.Value);
        }
    }

    public void DeleteMetadataEntry(object entry)
    {
        var metadataEntry = (MetadataEntry)entry;
        var metadata = _metadataRegistry.GetByName(metadataEntry.MetadataName);
        metadata?.DeleteMetadataEntry(metadataEntry.Subject, metadataEntry.Key);
    }

    private readonly IAppDataService _appDataService;
}