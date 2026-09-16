using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Workspace.Domain.Workspace.App;

public record AppId(Guid Value): EntityID<AppId>, IEntityID<AppId>
{
    public static AppId New() => new(Guid.NewGuid());
}
