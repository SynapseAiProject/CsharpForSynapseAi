using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Workspace.Domain.Workspace.Workspace;

public sealed class Workspace: AggregateRoot<WorkspaceId>
{
	public string Name { get; init; }

	public Workspace(WorkspaceId id, string name)
	{
		Id = id;
		Name = name;
	}
}
