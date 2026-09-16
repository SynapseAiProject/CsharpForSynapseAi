namespace BoundedContexts.Kernel.Tecnico.Domain;

public interface IEntity
{

}

public class Entity<EID>: IEquatable<Entity<EID>>, IEntity where EID : IEntityID
{
	public EID Id { get; protected set; } = default!;
	public bool Equals(Entity<EID>? other)
	{
		if (other is null) return false;
		if (other.Id.Equals(default(EID)) && Id.Equals(default(EID)))
			return ReferenceEquals(this, other);
		return GetType() == other.GetType() && Id.Equals(other.Id);
	}

	public override bool	Equals(object? obj) => Equals(obj as Entity<EID>);
	public override int GetHashCode() => Id.GetHashCode();
	public override string ToString() => $"Entity({Id})";
}
