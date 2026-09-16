namespace BoundedContexts.Kernel.Tecnico.Domain;

public interface IAggregateRoot
{

}
public class AggregateRoot<EID>: Entity<EID>, IAggregateRoot where EID : IEntityID
{
}
