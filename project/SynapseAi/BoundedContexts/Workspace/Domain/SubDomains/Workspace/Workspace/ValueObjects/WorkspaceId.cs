using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Workspace.Domain.Workspace.Workspace;

public record WorkspaceId(Guid Value): EntityID<WorkspaceId>, IEntityID<WorkspaceId>
{
    public static WorkspaceId New() => new(Guid.NewGuid());
}
