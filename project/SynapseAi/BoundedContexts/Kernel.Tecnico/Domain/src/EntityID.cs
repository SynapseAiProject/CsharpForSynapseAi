namespace BoundedContexts.Kernel.Tecnico.Domain;

public interface IEntityID { }

public interface IEntityID<Derived>: IValueObject<Derived>, IEntityID
	where Derived :
		EntityID<Derived>,
		IEntityID<Derived>
{

}

public abstract record EntityID<Derived>: ValueObject<Derived>
	where Derived :
		EntityID<Derived>,
		IEntityID<Derived>
{

}
