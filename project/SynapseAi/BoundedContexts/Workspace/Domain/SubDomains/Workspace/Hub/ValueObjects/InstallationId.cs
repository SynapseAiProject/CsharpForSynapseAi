using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Workspace.Domain.Workspace.Hub;

public record InstallationId(Guid Value): EntityID<InstallationId>, IEntityID<InstallationId>
{
    public static InstallationId New() => new(Guid.NewGuid());
}
