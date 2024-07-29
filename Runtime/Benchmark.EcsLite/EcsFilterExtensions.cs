using Leopotam.EcsLite;

namespace Benchmark.EcsLite
{

public static class EcsFilterExtensions
{
	public static bool HasEntity(this EcsFilter filter, int entity)
	{
		var sparseIndex = filter.GetSparseIndex();
		return entity >= 0 && entity < sparseIndex.Length && sparseIndex[entity] != 0;
	}
}

}
