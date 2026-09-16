namespace BoundedContexts.Kernel.Tecnico.Domain;

public interface IValueObject { }

public interface IValueObject<Derived>: IValueObject
	where Derived :
		ValueObject<Derived>,
		IValueObject<Derived>
{
	public abstract static Derived	New();
}

public abstract record ValueObject<Derived>
	where Derived :
		ValueObject<Derived>,
		IValueObject<Derived>
{

}
