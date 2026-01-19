using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.AppData;

public interface IAppDataService
{
    Task<FilterGraph> GetFilterGraph(Guid id);
    Task CreateFilterGraph(FilterGraph filterGraph);
    void DeleteFilterGraph(Guid id);
    Task<List<FilterGraph>> GetAllFilterGraphs();
}