using BoundedContexts.Kernel.Tecnico.Domain;
using BoundedContexts.Workspace.Domain.Workspace.Workspace;

namespace BoundedContexts.Workspace.Domain.Workspace.Hub;

public sealed class Hub: AggregateRoot<HubId>
{
    public WorkspaceId WorkspaceId { get; }
    public int Version { get; private set; }   // lock otimista — ver 07

    private readonly WatcheList<Installation> _installations = new();
    public IReadOnlyWatcheList<Installation> Installations => _installations.AsReadOnly();

    private Hub(HubId id, WorkspaceId workspaceId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Version = 0;
    }

    public static Hub CreateFor(WorkspaceId workspaceId)
        => new(HubId.New(), workspaceId);

    public Installation Install(App.App app)
    {
        if (_installations.Any(i => i.AppId == app.Id && i.Status == InstallationStatus.Active))
            throw new DomainException($"App {app.Id} já está instalado e ativo neste hub.");

        var installation = Installation.CreateFor(Id, app.Id);
        _installations.Add(installation);
        return installation;
    }
}
