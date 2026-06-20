using System.Collections.ObjectModel;
using HostManage.Models;

namespace HostManage.Services;

public interface IEnvironmentService
{
    ObservableCollection<HostEnvironment> Environments { get; }

    HostEnvironment? CurrentEnvironment { get; }

    Task CreateEnvironmentAsync(string name, string description, HostEnvironment? copyFrom = null);

    Task DeleteEnvironmentAsync(Guid envId);

    Task SwitchEnvironmentAsync(Guid envId);

    Task UpdateEnvironmentAsync(HostEnvironment env);

    Task<List<DiffItem>> CompareWithSystemHostsAsync(Guid envId);

    Task SaveEnvironmentsAsync();

    Task LoadEnvironmentsAsync();
}
