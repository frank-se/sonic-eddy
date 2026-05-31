using System;

namespace SonicEddy.Services.TraktorZ1;

public interface ITraktorZ1Service : IDisposable
{
    void Start(string hidrawPath);
    void Stop();

    event Action<TraktorZ1Side, int>? FilterSectionChanged;
}
