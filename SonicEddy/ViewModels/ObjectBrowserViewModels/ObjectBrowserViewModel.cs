using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using Fr.Wireplumber;
using Fr.Wireplumber.Model;
using Fr.Wireplumber.Model.Objects;
using ReactiveUI;
using SonicEddy.Models.ObjectBrowser;
using SonicEddy.Services.AppData;
using SonicEddy.ViewModels.ObjectDetailsViewModels;

namespace SonicEddy.ViewModels.ObjectBrowserViewModels;

public class ObjectBrowserViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly IAppDataService _appDataService;
    private readonly List<PipewireObject> _leftover = [];

    private PipewireObject? _selectedObject;

    private ObjectDetailsViewModelBase? _selectedObjectViewModel;

    public ObjectBrowserViewModel(IAppDataService appDataService)
    {
        _appDataService = appDataService;
        var clients = Wireplumber.ClientRegistry.Objects;
        Objects = new(clients.Select(PipewireObject.FromClient));

        ProcessDevices();
        ProcessNodes();
        ProcessPorts();

        this.WhenAnyValue(vm => vm.SelectedObject)
            .Subscribe(SetViewModelForSelectedObject);

        this.WhenActivated(disposables =>
        {
            Observable
                .FromEvent<Client>(
                    handler => Wireplumber.ClientRegistry.Added += handler,
                    handler => Wireplumber.ClientRegistry.Added -= handler)
                .Subscribe(HandleClientAddedEvent)
                .DisposeWith(disposables);

            Observable
                .FromEvent<Device>(
                    handler => Wireplumber.DeviceRegistry.Added += handler,
                    handler => Wireplumber.DeviceRegistry.Added -= handler)
                .Subscribe(HandleDeviceAddedEvent)
                .DisposeWith(disposables);

            Observable
                .FromEvent<Node>(
                    handler => Wireplumber.NodeRegistry.Added += handler,
                    handler => Wireplumber.NodeRegistry.Added -= handler)
                .Subscribe(HandleNodeAddedEvent)
                .DisposeWith(disposables);

            Observable
                .FromEvent<Port>(
                    handler => Wireplumber.PortRegistry.Added += handler,
                    handler => Wireplumber.PortRegistry.Added -= handler)
                .Subscribe(HandlePortAddedEvent)
                .DisposeWith(disposables);
        });
    }

    public ObjectDetailsViewModelBase? SelectedObjectViewModel
    {
        get => _selectedObjectViewModel;
        set => this.RaiseAndSetIfChanged(ref _selectedObjectViewModel, value);
    }

    public PipewireObject? SelectedObject
    {
        get => _selectedObject;
        set => this.RaiseAndSetIfChanged(ref _selectedObject, value);
    }

    public ObservableCollection<PipewireObject> Objects { get; set; }
    public ViewModelActivator Activator { get; } = new();

    private void SetViewModelForSelectedObject(PipewireObject? pipewireObject)
    {
        if (pipewireObject is null)
        {
            SelectedObjectViewModel = null;
        }
        else if (pipewireObject.Type == PipewireObjectType.Client)
        {
            var client = Wireplumber.ClientRegistry.Objects.FirstOrDefault(c =>
                c.ObjectId == pipewireObject.ObjectId);
            SelectedObjectViewModel = client != null
                ? ClientDetailsViewModel.FromClient(client)
                : null;
        }
        else if (pipewireObject.Type == PipewireObjectType.Device)
        {
            var device = Wireplumber.DeviceRegistry.Objects.FirstOrDefault(d =>
                d.ObjectId == pipewireObject.ObjectId);
            SelectedObjectViewModel = device != null
                ? DeviceDetailsViewModel.FromDevice(device)
                : null;
        }
        else if (pipewireObject.Type == PipewireObjectType.Node)
        {
            var node = Wireplumber.NodeRegistry.Objects.FirstOrDefault(n =>
                n.ObjectId == pipewireObject.ObjectId);
            SelectedObjectViewModel = node != null
                ? NodeDetailsViewModel.FromNode(node)
                : null;
        }
        else if (pipewireObject.Type == PipewireObjectType.Port)
        {
            var port = Wireplumber.PortRegistry.Objects.FirstOrDefault(p =>
                p.ObjectId == pipewireObject.ObjectId);
            SelectedObjectViewModel = port != null
                ? PortDetailsViewModel.FromPort(port)
                : null;
        }
    }

    private void HandleClientAddedEvent(Client client)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.InvokeAsync(() =>
                AddClient(client));
        else
            AddClient(client);
    }

    private void AddClient(Client client)
    {
        Objects.Add(PipewireObject.FromClient(client));

        if (_leftover.Count <= 0) return;

        var potentiallyChangedDevices = Wireplumber.DeviceRegistry.Objects
            .Where(d =>
                d.Client.Id == client.ObjectId &&
                _leftover.Any(l => l.ObjectSerial == d.ObjectSerial))
            .ToList();

        _leftover.RemoveAll(l =>
            potentiallyChangedDevices.Any(d =>
                d.ObjectSerial == l.ObjectSerial));
        foreach (var device in potentiallyChangedDevices)
            HandleDeviceAddedEvent(device);

        var potentiallyChangedNodes = Wireplumber.NodeRegistry.Objects
            .Where(n =>
                n.Client != null &&
                n.Client.Id == client.ObjectId &&
                _leftover.Any(l => l.ObjectSerial == n.ObjectSerial))
            .ToList();

        _leftover.RemoveAll(l =>
            potentiallyChangedNodes.Any(n => n.ObjectSerial == l.ObjectSerial));
        foreach (var node in potentiallyChangedNodes)
            HandleNodeAddedEvent(node);
    }

    private void HandleDeviceAddedEvent(Device device)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.InvokeAsync(() =>
                AddDevice(device));
        else
            AddDevice(device);
    }

    private void AddDevice(Device device)
    {
        var client = Objects.FirstOrDefault(c =>
            c.ObjectId == device.Client.Id);

        if (client == null)
        {
            _leftover.Add(PipewireObject.FromDevice(device));
            return;
        }

        client.Children.Add(PipewireObject.FromDevice(device));

        if (_leftover.Count <= 0) return;

        var potentiallyChangedNodes = Wireplumber.NodeRegistry.Objects
            .Where(n =>
                n.Device?.Id == device.ObjectId &&
                _leftover.Any(l => l.ObjectSerial == n.ObjectSerial))
            .ToList();
        _leftover.RemoveAll(l =>
            potentiallyChangedNodes.Any(n => n.ObjectSerial == l.ObjectSerial));
        foreach (var node in potentiallyChangedNodes)
            HandleNodeAddedEvent(node);
    }

    private void HandleNodeAddedEvent(Node node)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.InvokeAsync(() =>
                AddNode(node));
        else
            AddNode(node);
    }

    private void AddNode(Node node)
    {
        if (node.Client is null && node.Device is null)
        {
            Objects.Add(PipewireObject.FromNode(node));
        }
        else if (node.Device is null && node.Client != null)
        {
            var client =
                Objects.FirstOrDefault(o => o.ObjectId == node.Client.Id);
            if (client is null)
            {
                _leftover.Add(PipewireObject.FromNode(node));
                return;
            }

            client.Children.Add(PipewireObject.FromNode(node));
        }
        else if (node.Device != null && node.Client != null)
        {
            var client =
                Objects.FirstOrDefault(o => o.ObjectId == node.Client.Id);
            if (client is null)
            {
                _leftover.Add(PipewireObject.FromNode(node));
                return;
            }

            var device =
                client.Children.FirstOrDefault(d =>
                    d.ObjectId == node.Device.Id);
            if (device is null)
            {
                _leftover.Add(PipewireObject.FromNode(node));
                return;
            }

            device.Children.Add(PipewireObject.FromNode(node));
        }

        var potentiallyChangedPorts = Wireplumber.PortRegistry.Objects
            .Where(p => p.Node.Id == node.ObjectId)
            .ToList();
        _leftover.RemoveAll(l =>
            potentiallyChangedPorts.Any(p => p.ObjectSerial == l.ObjectSerial));
        foreach (var port in potentiallyChangedPorts)
            HandlePortAddedEvent(port);
    }

    private void HandlePortAddedEvent(Port port)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.InvokeAsync(() =>
                AddPort(port));
        else
            AddPort(port);
    }

    private void AddPort(Port port)
    {
        var nodes = Wireplumber.NodeRegistry.Objects;

        var pipewireNode =
            nodes.FirstOrDefault(n => n.ObjectId == port.Node.Id);
        if (pipewireNode != null)
        {
            if (pipewireNode.Device != null)
            {
                var client = Objects.FirstOrDefault(c =>
                    c.ObjectId == pipewireNode.Client!.Id);
                if (client != null)
                {
                    var device = client.Children.FirstOrDefault(d =>
                        d.ObjectId == pipewireNode.Device.Id);
                    if (device != null)
                    {
                        var node = device.Children.FirstOrDefault(n =>
                            n.ObjectId == port.Node.Id);
                        if (node != null)
                            node.Children.Add(
                                PipewireObject.FromPort(port));
                        else
                            _leftover.Add(PipewireObject.FromPort(port));
                    }
                    else
                    {
                        _leftover.Add(PipewireObject.FromPort(port));
                    }
                }
                else
                {
                    _leftover.Add(PipewireObject.FromPort(port));
                }
            }
            else if (pipewireNode.Client != null)
            {
                var client = Objects.FirstOrDefault(c =>
                    c.ObjectId == pipewireNode.Client.Id);
                if (client != null)
                {
                    var node =
                        client.Children.FirstOrDefault(n =>
                            n.ObjectId == port.Node.Id);
                    if (node != null)
                        node.Children.Add(PipewireObject.FromPort(port));
                    else
                        _leftover.Add(PipewireObject.FromPort(port));
                }
                else
                {
                    _leftover.Add(PipewireObject.FromPort(port));
                }
            }
            else
            {
                var node =
                    Objects.FirstOrDefault(n => n.ObjectId == port.Node.Id);
                if (node != null)
                    node.Children.Add(PipewireObject.FromPort(port));
                else
                    _leftover.Add(PipewireObject.FromPort(port));
            }
        }
        else
        {
            _leftover.Add(PipewireObject.FromPort(port));
        }
    }

    private void ProcessPorts()
    {
        var ports = Wireplumber.PortRegistry.Objects;
        foreach (var port in ports) AddPort(port);
    }

    private void ProcessNodes()
    {
        var nodes = Wireplumber.NodeRegistry.Objects;
        foreach (var node in nodes)
            if (node.Client == null)
            {
                Objects.Add(PipewireObject.FromNode(node));
            }
            else
            {
                var client =
                    Objects.FirstOrDefault(c => c.ObjectId == node.Client.Id);
                if (client != null)
                {
                    if (node.Device != null)
                    {
                        var device =
                            client.Children.FirstOrDefault(d =>
                                d.ObjectId == node.Device.Id);
                        if (device != null)
                            device.Children.Add(PipewireObject.FromNode(node));
                        else
                            _leftover.Add(PipewireObject.FromNode(node));
                    }
                    else
                    {
                        client.Children.Add(PipewireObject.FromNode(node));
                    }
                }
                else
                {
                    _leftover.Add(PipewireObject.FromNode(node));
                }
            }
    }

    private void ProcessDevices()
    {
        var devices = Wireplumber.DeviceRegistry.Objects;
        foreach (var device in devices)
        {
            var client =
                Objects.FirstOrDefault(c => c.ObjectId == device.Client.Id);
            if (client != null)
                client.Children.Add(PipewireObject.FromDevice(device));
            else
                _leftover.Add(PipewireObject.FromDevice(device));
        }
    }
}