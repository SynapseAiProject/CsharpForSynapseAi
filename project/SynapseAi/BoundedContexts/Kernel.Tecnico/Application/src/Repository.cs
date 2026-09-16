using BoundedContexts.Kernel.Tecnico.Domain;

namespace BoundedContexts.Kernel.Tecnico.Application;

public interface Repository<AggRoot> where AggRoot : IAggregateRoot
{
}
