using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using Fr.Wireplumber;
using Fr.Wireplumber.Model;
using ReactiveUI;
using SonicEddy.Models.ObjectBrowser;

namespace SonicEddy.ViewModels;

public class ObjectBrowserViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly List<PipewireObject> _leftover = [];

    public ObjectBrowserViewModel()
    {
        var clients = Wireplumber.Clients;
        Objects = new(clients.Select(PipewireObject.FromClient));

        ProcessDevices();
        ProcessNodes();
        ProcessPorts();

        this.WhenActivated(disposables =>
        {
            Observable
                .FromEvent<Client>(
                    handler => Wireplumber.ClientAdded += handler,
                    handler => Wireplumber.ClientAdded -= handler)
                .Subscribe(HandleClientAddedEvent)
                .DisposeWith(disposables);

            Observable
                .FromEvent<Device>(
                    handler => Wireplumber.DeviceAdded += handler,
                    handler => Wireplumber.DeviceAdded -= handler)
                .Subscribe(HandleDeviceAddedEvent)
                .DisposeWith(disposables);

            Observable
                .FromEvent<Node>(
                    handler => Wireplumber.NodeAdded += handler,
                    handler => Wireplumber.NodeAdded -= handler)
                .Subscribe(HandleNodeAddedEvent)
                .DisposeWith(disposables);

            Observable
                .FromEvent<Port>(
                    handler => Wireplumber.PortAdded += handler,
                    handler => Wireplumber.PortAdded -= handler)
                .Subscribe(HandlePortAddedEvent)
                .DisposeWith(disposables);
        });
    }

    public ObservableCollection<PipewireObject> Objects { get; set; }

    public ViewModelActivator Activator { get; } = new();

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

        var potentialChanges = new List<PipewireObject>();

        foreach (var leftover in _leftover.ToList())
            if (leftover.Type == PipewireObjectType.Device)
            {
                var device = Wireplumber.Devices.First(d =>
                    d.ObjectSerial == leftover.ObjectSerial);

                if (device.Client.Id != client.ObjectId) continue;

                potentialChanges.Add(leftover);
                _leftover.Remove(leftover);
            }
            else if (leftover.Type == PipewireObjectType.Node)
            {
                var node = Wireplumber.Nodes.First(n =>
                    n.ObjectSerial == leftover.ObjectSerial);

                if (node.Client?.Id != client.ObjectId) continue;

                potentialChanges.Add(leftover);
                _leftover.Remove(leftover);
            }

        foreach (var potentialChange in potentialChanges)
            if (potentialChange.Type == PipewireObjectType.Device)
            {
                var device = Wireplumber.Devices.First(d =>
                    d.ObjectSerial == potentialChange.ObjectSerial);
                HandleDeviceAddedEvent(device);
            }
            else if (potentialChange.Type == PipewireObjectType.Node)
            {
                var node = Wireplumber.Nodes.First(n =>
                    n.ObjectSerial == potentialChange.ObjectSerial);
                HandleNodeAddedEvent(node);
            }
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

        var potentialChanges = new List<PipewireObject>();
        var nodes =
            Wireplumber.Nodes.Where(n => n.Device?.Id == device.ObjectId)
                .ToList();

        foreach (var nodeObject in nodes.Select(node =>
                         Objects.FirstOrDefault(n =>
                             n.Type == PipewireObjectType.Node &&
                             n.ObjectId == node.ObjectId))
                     .OfType<PipewireObject>())
        {
            _leftover.Remove(nodeObject);
            potentialChanges.Add(nodeObject);
        }

        foreach (var potentialChange in potentialChanges)
            HandleNodeAddedEvent(nodes.First(n =>
                n.ObjectId == potentialChange.ObjectId));
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
    }

    private void ProcessPorts()
    {
        var ports = Wireplumber.Ports;
        var nodes = Wireplumber.Nodes;

        foreach (var port in ports)
        {
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
    }

    private void ProcessNodes()
    {
        var nodes = Wireplumber.Nodes;
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
        var devices = Wireplumber.Devices;
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