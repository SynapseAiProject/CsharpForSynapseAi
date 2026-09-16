using System.Text.RegularExpressions;
using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Workspace.Domain.Workspace.App;

public record Slug: ValueObject<Slug>, IValueObject<Slug>
{
	public string Value { get; init; }
	private Slug(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			Value = default!;
			return;
		}
		Value = Regex.Replace(name.Trim().ToLower(), @"\s+", "-");
	}
    public static Slug New() => new("");
	public static Slug New(string name) => new(name);
}
