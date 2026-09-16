using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Workspace.Domain.Workspace.Hub;

public record HubId(Guid Value): EntityID<HubId>, IEntityID<HubId>
{
    public static HubId New() => new(Guid.NewGuid());
}
