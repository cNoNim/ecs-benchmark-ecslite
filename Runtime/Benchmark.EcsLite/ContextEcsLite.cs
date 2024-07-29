using System;
using System.Buffers;
using Benchmark.Core;
using Benchmark.Core.Components;
using Benchmark.Core.Hash;
using Benchmark.Core.Random;
using Leopotam.EcsLite;

namespace Benchmark.EcsLite
{

public class ContextEcsLite : ContextBase
{
	private EcsSystems? _ecsSystems;
	private EcsWorld?   _world;

	public ContextEcsLite()
		: base("EcsLite") {}

	protected override void DoSetup()
	{
		var world = _world = new EcsWorld();
		_ecsSystems = new EcsSystems(world);
		_ecsSystems.Add(new SpawnSystem(world));
		_ecsSystems.Add(new RespawnSystem(world));
		_ecsSystems.Add(new KillSystem(world));
		_ecsSystems.Add(new RenderSystem(world, Framebuffer));
		_ecsSystems.Add(new SpriteSystem(world));
		_ecsSystems.Add(new DamageSystem(world));
		_ecsSystems.Add(new AttackSystem(world));
		_ecsSystems.Add(new MovementSystem(world));
		_ecsSystems.Add(new UpdateVelocitySystem(world));
		_ecsSystems.Add(new UpdateDataSystem(world));
		_ecsSystems.Init();

		var spawnPool = world.GetPool<Spawn>();
		var dataPool  = world.GetPool<Data>();
		var unitPool  = world.GetPool<Unit>();
		for (var i = 0; i < EntityCount; ++i)
		{
			var entity = world.NewEntity();
			spawnPool.Add(entity);
			dataPool.Add(entity);
			unitPool.Add(entity) = new Unit
			{
				Id   = (uint) i,
				Seed = (uint) i,
			};
		}
	}

	protected override void DoRun(int tick) =>
		_ecsSystems?.Run();

	protected override void DoCleanup()
	{
		_ecsSystems?.Destroy();
		_ecsSystems = null;
		_world?.Destroy();
		_world = null;
	}

	private class SpawnSystem : IEcsRunSystem
	{
		private readonly EcsPool<Damage>       _damagePool;
		private readonly EcsPool<Data>         _dataPool;
		private readonly EcsFilter             _filter;
		private readonly EcsPool<Health>       _healthPool;
		private readonly EcsPool<Unit.Hero>    _heroPool;
		private readonly EcsPool<Unit.Monster> _monsterPool;
		private readonly EcsPool<Unit.NPC>     _npcPool;
		private readonly EcsPool<Position>     _positionPool;
		private readonly EcsPool<Spawn>        _spawnPool;
		private readonly EcsPool<Sprite>       _spritePool;
		private readonly EcsPool<Unit>         _unitPool;
		private readonly EcsPool<Velocity>     _velocityPool;

		public SpawnSystem(EcsWorld world)
		{
			_filter = world.Filter<Unit>()
						   .Inc<Data>()
						   .Inc<Spawn>()
						   .End();
			_unitPool     = world.GetPool<Unit>();
			_dataPool     = world.GetPool<Data>();
			_spawnPool    = world.GetPool<Spawn>();
			_npcPool      = world.GetPool<Unit.NPC>();
			_heroPool     = world.GetPool<Unit.Hero>();
			_monsterPool  = world.GetPool<Unit.Monster>();
			_healthPool   = world.GetPool<Health>();
			_damagePool   = world.GetPool<Damage>();
			_spritePool   = world.GetPool<Sprite>();
			_positionPool = world.GetPool<Position>();
			_velocityPool = world.GetPool<Velocity>();
		}

		public void Run(IEcsSystems _)
		{
			foreach (var entity in _filter)
			{
				switch (SpawnUnit(
							in _dataPool.Get(entity),
							ref _unitPool.Get(entity),
							out _healthPool.Add(entity),
							out _damagePool.Add(entity),
							out _spritePool.Add(entity),
							out _positionPool.Add(entity),
							out _velocityPool.Add(entity)))
				{
				case UnitType.NPC:
					_npcPool.Add(entity);
					break;
				case UnitType.Hero:
					_heroPool.Add(entity);
					break;
				case UnitType.Monster:
					_monsterPool.Add(entity);
					break;
				}

				_spawnPool.Del(entity);
			}
		}
	}

	private class UpdateDataSystem : IEcsRunSystem
	{
		private readonly EcsPool<Data> _dataPool;
		private readonly EcsFilter     _filter;

		public UpdateDataSystem(EcsWorld world)
		{
			_filter = world.Filter<Data>()
						   .End();
			_dataPool = world.GetPool<Data>();
		}

		public void Run(IEcsSystems _)
		{
			foreach (var entity in _filter)
				UpdateDataSystemForEach(ref _dataPool.Get(entity));
		}
	}

	private class UpdateVelocitySystem : IEcsRunSystem
	{
		private readonly EcsPool<Data>     _dataPool;
		private readonly EcsFilter         _filter;
		private readonly EcsPool<Position> _positionPool;
		private readonly EcsPool<Unit>     _unitPool;
		private readonly EcsPool<Velocity> _velocityPool;

		public UpdateVelocitySystem(EcsWorld world)
		{
			_filter = world.Filter<Velocity>()
						   .Inc<Unit>()
						   .Inc<Data>()
						   .Inc<Position>()
						   .Exc<Dead>()
						   .End();
			_velocityPool = world.GetPool<Velocity>();
			_unitPool     = world.GetPool<Unit>();
			_dataPool     = world.GetPool<Data>();
			_positionPool = world.GetPool<Position>();
		}

		public void Run(IEcsSystems _)
		{
			foreach (var entity in _filter)
				UpdateVelocitySystemForEach(
					ref _velocityPool.Get(entity),
					ref _unitPool.Get(entity),
					in _dataPool.Get(entity),
					in _positionPool.Get(entity));
		}
	}

	private class MovementSystem : IEcsRunSystem
	{
		private readonly EcsFilter         _filter;
		private readonly EcsPool<Position> _positionPool;
		private readonly EcsPool<Velocity> _velocityPool;

		public MovementSystem(EcsWorld world)
		{
			_filter = world.Filter<Position>()
						   .Inc<Velocity>()
						   .Exc<Dead>()
						   .End();
			_positionPool = world.GetPool<Position>();
			_velocityPool = world.GetPool<Velocity>();
		}

		public void Run(IEcsSystems _)
		{
			foreach (var entity in _filter)
				MovementSystemForEach(ref _positionPool.Get(entity), in _velocityPool.Get(entity));
		}
	}

	private class AttackSystem : IEcsRunSystem
	{
		private readonly EcsPool<Attack<EcsPackedEntity>> _attackPool;
		private readonly EcsPool<Damage>                  _damagePool;
		private readonly EcsPool<Data>                    _dataPool;
		private readonly EcsFilter                        _filter;
		private readonly EcsPool<Position>                _positionPool;
		private readonly EcsPool<Unit>                    _unitPool;
		private readonly EcsWorld                         _world;

		public AttackSystem(EcsWorld world)
		{
			_world = world;
			_filter = world.Filter<Unit>()
						   .Inc<Data>()
						   .Inc<Damage>()
						   .Inc<Position>()
						   .Exc<Spawn>()
						   .Exc<Dead>()
						   .End();
			_unitPool     = world.GetPool<Unit>();
			_dataPool     = world.GetPool<Data>();
			_damagePool   = world.GetPool<Damage>();
			_positionPool = world.GetPool<Position>();
			_attackPool   = world.GetPool<Attack<EcsPackedEntity>>();
		}

		public void Run(IEcsSystems _)
		{
			var count   = _filter.GetEntitiesCount();
			var keys    = ArrayPool<uint>.Shared.Rent(count);
			var targets = ArrayPool<Target<int>>.Shared.Rent(count);
			FillTargets(keys, targets);
			Array.Sort(
				keys,
				targets,
				0,
				count);
			CreateAttacks(targets, count);
			ArrayPool<uint>.Shared.Return(keys);
			ArrayPool<Target<int>>.Shared.Return(targets);
		}

		private void FillTargets(uint[] keys, Target<int>[] targets)
		{
			var i = 0;
			foreach (var entity in _filter)
			{
				var index = i++;
				keys[index] = _unitPool.Get(entity)
									   .Id;
				targets[index] = new Target<int>(entity, _positionPool.Get(entity));
			}
		}

		private void CreateAttacks(Target<int>[] targets, int count)
		{
			foreach (var entity in _filter)
			{
				ref readonly var damage = ref _damagePool.Get(entity);
				if (damage.Cooldown <= 0)
					continue;

				ref var          unit = ref _unitPool.Get(entity);
				ref readonly var data = ref _dataPool.Get(entity);
				var              tick = data.Tick - unit.SpawnTick;
				if (tick % damage.Cooldown != 0)
					continue;

				ref readonly var position     = ref _positionPool.Get(entity);
				var              generator    = new RandomGenerator(unit.Seed);
				var              index        = generator.Random(ref unit.Counter, count);
				var              target       = targets[index];
				var              attackEntity = _world.NewEntity();
				_attackPool.Add(attackEntity) = new Attack<EcsPackedEntity>
				{
					Target = _world.PackEntity(target.Entity),
					Damage = damage.Attack,
					Ticks  = Common.AttackTicks(position.V, target.Position),
				};
			}
		}
	}

	private class DamageSystem : IEcsRunSystem
	{
		private readonly EcsFilter                        _attackFilter;
		private readonly EcsPool<Attack<EcsPackedEntity>> _attackPool;
		private readonly EcsPool<Damage>                  _damagePool;
		private readonly EcsFilter                        _filter;
		private readonly EcsPool<Health>                  _healthPool;
		private readonly EcsWorld                         _world;

		public DamageSystem(EcsWorld world)
		{
			_world = world;
			_attackFilter = world.Filter<Attack<EcsPackedEntity>>()
								 .End();
			_filter = world.Filter<Health>()
						   .Inc<Damage>()
						   .Exc<Dead>()
						   .End();
			_attackPool = world.GetPool<Attack<EcsPackedEntity>>();
			_healthPool = world.GetPool<Health>();
			_damagePool = world.GetPool<Damage>();
		}

		public void Run(IEcsSystems _)
		{
			foreach (var entity in _attackFilter)
			{
				ref var attack = ref _attackPool.Get(entity);
				if (attack.Ticks-- > 0)
					continue;

				var target       = attack.Target;
				var attackDamage = attack.Damage;

				_world.DelEntity(entity);

				if (!target.Unpack(_world, out var targetEntity)
				 || !_filter.HasEntity(targetEntity))
					continue;

				ref var          health      = ref _healthPool.Get(targetEntity);
				ref readonly var damage      = ref _damagePool.Get(targetEntity);
				var              totalDamage = attackDamage - damage.Defence;
				health.Hp -= totalDamage;
			}
		}
	}

	private class KillSystem : IEcsRunSystem
	{
		private readonly EcsPool<Data>   _dataPool;
		private readonly EcsPool<Dead>   _deadPool;
		private readonly EcsFilter       _filter;
		private readonly EcsPool<Health> _healthPool;
		private readonly EcsPool<Unit>   _unitPool;

		public KillSystem(EcsWorld world)
		{
			_filter = world.Filter<Unit>()
						   .Inc<Health>()
						   .Inc<Data>()
						   .Exc<Dead>()
						   .End();
			_healthPool = world.GetPool<Health>();
			_unitPool   = world.GetPool<Unit>();
			_dataPool   = world.GetPool<Data>();
			_deadPool   = world.GetPool<Dead>();
		}

		public void Run(IEcsSystems _)
		{
			foreach (var entity in _filter)
			{
				ref readonly var health = ref _healthPool.Get(entity);
				if (health.Hp > 0)
					continue;

				ref var unit = ref _unitPool.Get(entity);
				_deadPool.Add(entity);
				ref readonly var data = ref _dataPool.Get(entity);
				unit.RespawnTick = data.Tick + RespawnTicks;
			}
		}
	}

	private class SpriteSystem : IEcsRunSystem
	{
		private readonly EcsFilter       _deadFilter;
		private readonly EcsFilter       _heroFilter;
		private readonly EcsFilter       _monsterFilter;
		private readonly EcsFilter       _npcFilter;
		private readonly EcsFilter       _spawnFilter;
		private readonly EcsPool<Sprite> _spritePool;

		public SpriteSystem(EcsWorld world)
		{
			_spawnFilter = world.Filter<Sprite>()
								.Inc<Spawn>()
								.End();
			_deadFilter = world.Filter<Sprite>()
							   .Inc<Dead>()
							   .End();
			_npcFilter = world.Filter<Sprite>()
							  .Inc<Unit.NPC>()
							  .Exc<Spawn>()
							  .Exc<Dead>()
							  .End();
			_heroFilter = world.Filter<Sprite>()
							   .Inc<Unit.Hero>()
							   .Exc<Spawn>()
							   .Exc<Dead>()
							   .End();
			_monsterFilter = world.Filter<Sprite>()
								  .Inc<Unit.Monster>()
								  .Exc<Spawn>()
								  .Exc<Dead>()
								  .End();
			_spritePool = world.GetPool<Sprite>();
		}

		public void Run(IEcsSystems _)
		{
			ForEachSprite(_spawnFilter,   SpriteMask.Spawn);
			ForEachSprite(_deadFilter,    SpriteMask.Grave);
			ForEachSprite(_npcFilter,     SpriteMask.NPC);
			ForEachSprite(_heroFilter,    SpriteMask.Hero);
			ForEachSprite(_monsterFilter, SpriteMask.Monster);
		}

		private void ForEachSprite(EcsFilter filter, SpriteMask sprite)
		{
			foreach (var entity in filter)
				_spritePool.Get(entity)
						   .Character = sprite;
		}
	}

	private class RenderSystem : IEcsRunSystem
	{
		private readonly EcsPool<Data>     _datas;
		private readonly EcsFilter         _filter;
		private readonly Framebuffer       _framebuffer;
		private readonly EcsPool<Position> _positions;
		private readonly EcsPool<Sprite>   _sprites;
		private readonly EcsPool<Unit>     _units;

		public RenderSystem(EcsWorld world, Framebuffer framebuffer)
		{
			_framebuffer = framebuffer;
			_filter = world.Filter<Position>()
						   .Inc<Sprite>()
						   .Inc<Unit>()
						   .Inc<Data>()
						   .End();
			_positions = world.GetPool<Position>();
			_sprites   = world.GetPool<Sprite>();
			_units     = world.GetPool<Unit>();
			_datas     = world.GetPool<Data>();
		}

		public void Run(IEcsSystems _)
		{
			foreach (var entity in _filter)
				RenderSystemForEach(
					_framebuffer,
					in _positions.Get(entity),
					in _sprites.Get(entity),
					in _units.Get(entity),
					in _datas.Get(entity));
		}
	}

	private class RespawnSystem : IEcsRunSystem
	{
		private readonly EcsPool<Data>  _dataPool;
		private readonly EcsFilter      _filter;
		private readonly EcsPool<Spawn> _spawnPool;
		private readonly EcsPool<Unit>  _unitPool;
		private readonly EcsWorld       _world;

		public RespawnSystem(EcsWorld world)
		{
			_world = world;
			_filter = world.Filter<Unit>()
						   .Inc<Data>()
						   .Inc<Dead>()
						   .End();
			_spawnPool = world.GetPool<Spawn>();
			_dataPool  = world.GetPool<Data>();
			_unitPool  = world.GetPool<Unit>();
		}

		public void Run(IEcsSystems _)
		{
			foreach (var entity in _filter)
			{
				ref readonly var unit = ref _unitPool.Get(entity);
				ref readonly var data = ref _dataPool.Get(entity);
				if (data.Tick < unit.RespawnTick)
					continue;

				var newEntity = _world.NewEntity();
				_spawnPool.Add(newEntity);
				_dataPool.Add(newEntity) = data;
				_unitPool.Add(newEntity) = new Unit
				{
					Id   = unit.Id | (uint) data.Tick << 16,
					Seed = StableHash32.Hash(unit.Seed, unit.Counter),
				};
				_world.DelEntity(entity);
			}
		}
	}
}

}
