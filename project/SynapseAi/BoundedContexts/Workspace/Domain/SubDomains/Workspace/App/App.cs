using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Workspace.Domain.Workspace.App;

public sealed class App: AggregateRoot<AppId>
{
	public Slug		Slug { get; }
	public string	Name { get; }

	public App(AppId id, string name, Slug? slug = null)
	{
		Id = id;
		Name = name;
		if (slug is null)
			Slug = Slug.New(Name);
		else
			Slug = slug;
	}
}
