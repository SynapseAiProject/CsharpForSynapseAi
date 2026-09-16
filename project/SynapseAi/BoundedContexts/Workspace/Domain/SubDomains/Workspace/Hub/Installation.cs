using BoundedContexts.Kernel.Tecnico.Domain;
using BoundedContexts.Workspace.Domain.Workspace.App;

namespace BoundedContexts.Workspace.Domain.Workspace.Hub;

public enum InstallationStatus
{
	Active,
}

public sealed class Installation: Entity<InstallationId>
{
	public AppId				AppId	{ get; }
	public HubId				HubId	{ get; }
	public InstallationStatus	Status	{ get; }

	// private Installation(InstallationId id, AppId appId, HubId hubId, InstallationStatus status)
	// {
	// 	Id = id;
	// 	AppId = appId;
	// 	HubId = hubId;
	// 	Status = status;
	// }

	private Installation(HubId hubId, AppId appId)
	{
		Id = InstallationId.New();
		AppId = appId;
		HubId = hubId;
		Status = default;
	}

	internal static Installation CreateFor(HubId hubId, AppId appId)
	{
		return new Installation(hubId, appId);
	}
}
