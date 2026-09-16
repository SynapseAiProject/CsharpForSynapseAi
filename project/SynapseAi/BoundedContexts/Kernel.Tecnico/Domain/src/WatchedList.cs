using System.Collections;

namespace BoundedContexts.Kernel.Tecnico.Domain;

public interface IWatcheList
{

}

public interface IReadOnlyWatcheList<TEntity> : IEnumerable<TEntity>
	where TEntity : IEntity
{
	IReadOnlySet<TEntity> Current { get; }
	IReadOnlySet<TEntity> Added { get; }
	IReadOnlySet<TEntity> Removed { get; }
	IReadOnlySet<TEntity> Updated { get; }

	bool HasAdditions(TEntity item);
	bool HasUpdates(TEntity item);
	bool HasRemovals(TEntity item);
}

public interface IWatcheList<TEntity> : IReadOnlyWatcheList<TEntity>, IWatcheList
	where TEntity : IEntity
{
	void Add(TEntity item);
	void Add(IEnumerable<TEntity> items);

	void Remove(TEntity item);
	void Remove(IEnumerable<TEntity> items);

	void Update(TEntity item);
	void Update(IEnumerable<TEntity> items);
}


public class WatcheList<TEntity>: IEnumerable<TEntity>, IWatcheList<TEntity> where TEntity : IEntity
{
	private readonly HashSet<TEntity>	_initial = new();
	private readonly HashSet<TEntity>	_current = new();
	private readonly HashSet<TEntity>	_added = new();
	private readonly HashSet<TEntity>	_removed = new();
	private readonly HashSet<TEntity>	_updated = new();

	public IReadOnlySet<TEntity>	Current { get => _current.AsReadOnly(); }
	public IReadOnlySet<TEntity>	Added { get => _added.AsReadOnly(); }
	public IReadOnlySet<TEntity>	Removed { get => _removed.AsReadOnly(); }
	public IReadOnlySet<TEntity>	Updated { get => _updated.AsReadOnly(); }

	public IReadOnlyWatcheList<TEntity>	AsReadOnly() => this;

	public WatcheList(IEnumerable<TEntity>? begin = null)
	{
		if (begin is null)
		{
			_current = new();
			_initial = new();
			return;
		}
		_current = [..begin];
		_initial = [..begin];
	}

	public IEnumerator<TEntity> GetEnumerator() => _current.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	bool IsInitial(TEntity item) => _initial.Contains(item);
	bool IsAdded(TEntity item) => _added.Contains(item);
	bool IsRemoved(TEntity item) => _removed.Contains(item);
	bool IsCurrent(TEntity item) => _current.Contains(item);

	public bool HasAdditions(TEntity item) => _added.Count > 0;
	public bool HasUpdates(TEntity item) => _updated.Count > 0;
	public bool HasRemovals(TEntity item) => _removed.Count > 0;

	public void	Add(TEntity item)
	{
		if (IsAdded(item))
			return;
		if (IsRemoved(item))
		{
			_removed.Remove(item);
			if (IsInitial(item))
				_updated.Add(item);
		}
		_current.Add(item);
		_added.Add(item);
	}

	public void	Add(IEnumerable<TEntity> items)
	{
		foreach (var item in items)
			Add(item);
	}

	public void Remove(TEntity item)
	{
		if (IsRemoved(item) || !IsCurrent(item))
			return;
		_added.Remove(item);
		_current.Remove(item);
		_updated.Remove(item);
		_removed.Add(item);
	}

	public void	Remove(IEnumerable<TEntity> items)
	{
		foreach (var item in items)
			Remove(item);
	}

	public void Update(TEntity item)
	{
		if (IsCurrent(item))
			return;
		_updated.Add(item);
	}

	public void	Update(IEnumerable<TEntity> items)
	{
		foreach (var item in items)
			Update(item);
	}
}
