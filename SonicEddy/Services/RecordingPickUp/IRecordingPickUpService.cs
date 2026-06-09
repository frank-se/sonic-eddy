using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fr.Sonic.Model.Objects;

namespace SonicEddy.Services.RecordingPickUp;

public interface IRecordingPickUpService
{
    List<RecordingPickUp> PickUps { get; }
    event Action<RecordingPickUp>? Added;
    Task InitializeAsync();
    Task AddPickUp(string name, Node sourceNode, Node destNode);
    void SetActive(Guid id, bool active);
    void SetTrim(Guid id, double trim);
}
