﻿using System.Collections;
using UnityEngine;
using System.Xml;
using System;
using System.Collections.Generic;
using ShootAR.Enemies;

namespace ShootAR
{

	public class Spawner : MonoBehaviour
	{
		private static XmlReader xmlPattern;

		[SerializeField] private Type objectToSpawn;
		/// <summary>
		/// Reference to the type of <see cref="Spawnable"/> prefab to copy
		/// while spawnning.
		/// </summary>
		public Type ObjectToSpawn {
			get { return objectToSpawn; }
			private set { objectToSpawn = value; }
		}
		/// <summary>
		/// The time interval between each spawn in seconds.
		/// </summary>
		public float SpawnRate { get; set; }
		/// <summary>
		/// The initial delay before spawning the first object in seconds.
		/// </summary>
		/// <remarks>
		/// Mind the additional waiting time from <see cref="SpawnRate"/>.
		/// </remarks>
		public float InitialDelay { get; private set; }
		[SerializeField] private float maxDistanceToSpawn, minDistanceToSpawn;
		/// <summary>
		/// Maximum distance away from player that <see cref="ObjectToSpawn"/> is
		/// allowed to spawn.
		/// </summary>
		public float MaxDistanceToSpawn {
			get { return maxDistanceToSpawn; }
			private set { maxDistanceToSpawn = value; }
		}
		/// <summary>
		/// Minimum distance away from player that <see cref="ObjectToSpawn"/> is
		/// allowed to spawn.
		/// </summary>
		public float MinDistanceToSpawn {
			get { return minDistanceToSpawn; }
			private set { minDistanceToSpawn = value; }
		}
		/// <summary>
		/// Number Of ObjectToSpawn objects to spawn.
		/// </summary>
		public int SpawnLimit { get; private set; }
		/// <summary>
		/// Count of how many instances of <see cref="ObjectToSpawn"/> were spawned.
		/// </summary>
		/// <remarks>
		/// Resets every time StartSpawning is called.
		/// </remarks>
		public int SpawnCount { get; private set; }
		public bool IsSpawning { get; private set; } = false;

		private AudioSource audioPlayer;
		private static GameState gameState;
#pragma warning disable CS0649
		[SerializeField] private GameObject portal;
		[SerializeField] private AudioClip spawnSfx;
#pragma warning restore CS0649

		private void Awake() {
			//Initial value should not be 0 to refrain from enabling
			//"Game Over" state when the game has just started.
			if (SpawnLimit == 0) SpawnLimit = -1;
		}

		public static Spawner Create(
				Type objectToSpawn = null, int spawnLimit = default,
				float initialDelay = default, float spawnRate = default,
				float maxDistanceToSpawn = default,
				float minDistanceToSpawn = default,
				GameState gameState = null) {
			var o = new GameObject(nameof(Spawner)).AddComponent<Spawner>();

			o.ObjectToSpawn = objectToSpawn;
			o.SpawnLimit = spawnLimit;
			o.SpawnRate = spawnRate;
			o.MaxDistanceToSpawn = maxDistanceToSpawn;
			o.MinDistanceToSpawn = minDistanceToSpawn;
			Spawner.gameState = gameState;

			// Since Create() is not an actual constructor, when object o is
			// created, o.gameState is null and the code in OnEnable() won't
			// run.
			o.OnEnable();

			return o;
		}

		private void Start() {
			if (spawnSfx != null) {
				audioPlayer = gameObject.AddComponent<AudioSource>();
				audioPlayer.clip = spawnSfx;
				audioPlayer.volume = 0.2f;
			}

			if (gameState is null)
				gameState = FindObjectOfType<GameState>();
		}

		private void OnEnable() {
			if (gameState != null) {
				gameState.OnGameOver += StopSpawning;
				gameState.OnRoundWon += StopSpawning;
			}
		}

		private void OnDisable() {
			if (gameState != null) {
				gameState.OnGameOver -= StopSpawning;
				gameState.OnRoundWon -= StopSpawning;
			}
		}

		/// <summary>
		/// Spawn objects until the spawn-limit is reached.
		/// </summary>
		/// <remarks>
		/// Automaticaly called through <see cref="StartSpawning"/>. Iteration
		/// will stop when the limit defined by <see cref="SpawnLimit"/> is
		/// reached or can be manually stopped, using
		/// <see cref="StopSpawning"/>.
		///
		/// The spawner changes its position and rotation before spawning an
		/// object. The object is spawned at the same position and with the same
		/// rotation as the spawner.
		///
		/// A pool containing copies of <see cref="objectToSpawn"/> is required.
		/// </remarks>
		/// <seealso cref="Spawnable.Pool{T}"/>
		private IEnumerator Spawn() {
			yield return new WaitForSeconds(InitialDelay);
			while (IsSpawning) {
				yield return new WaitForSeconds(SpawnRate);

				/* IsSpawning is checked here in case StopSpawning() is called
				 * while being in the middle of this function call. */
				if (!IsSpawning) break;
				if (SpawnCount >= Spawnable.GLOBAL_SPAWN_LIMIT) continue;

				float r = UnityEngine.Random.Range(minDistanceToSpawn, maxDistanceToSpawn);
				float theta = UnityEngine.Random.Range(0f, Mathf.PI);
				float fi = UnityEngine.Random.Range(0f, 2 * Mathf.PI);
				float x = r * Mathf.Sin(theta) * Mathf.Cos(fi);
				float y = r * Mathf.Sin(theta) * Mathf.Sin(fi);
				float z = r * Mathf.Cos(theta);

				transform.localPosition = new Vector3(x, y, z);
				transform.localRotation = Quaternion.LookRotation(
						-transform.localPosition);

				//Spawn special effects
				if (portal != null)
					Instantiate(portal,
						transform.localPosition, transform.localRotation);
				if (spawnSfx != null)
					audioPlayer.Play();

				/* Make checks for each and every type of Spawnable, because
				 * a class inheriting from MonoBehaviour cannot be a generic
				 * class. */
				if (objectToSpawn == typeof(Enemies.Crasher))
					InstantiateSpawnable<Enemies.Crasher>();
				else if (objectToSpawn == typeof(Enemies.Drone))
					InstantiateSpawnable<Enemies.Drone>();
				else if (objectToSpawn == typeof(BulletCapsule))
					InstantiateSpawnable<BulletCapsule>();
				else if (objectToSpawn == typeof(ArmorCapsule))
					InstantiateSpawnable<ArmorCapsule>();
				else if (objectToSpawn == typeof(HealthCapsule))
					InstantiateSpawnable<HealthCapsule>();
				else if (objectToSpawn == typeof(PowerUpCapsule))
					InstantiateSpawnable<PowerUpCapsule>();
				else
					throw new UnityException("Unrecognised type of Spawnable");

				SpawnCount++;

				if (SpawnCount == SpawnLimit) StopSpawning();
			}
		}

		private void InstantiateSpawnable<T>() where T : Spawnable {
			var spawned = Spawnable.Pool<T>.RequestObject();

			spawned.transform.position = transform.localPosition;
			spawned.transform.rotation = transform.localRotation;
			spawned.gameObject.SetActive(true);
		}

		/// <summary>
		/// Start a <see cref="Spawn"/> coroutine.
		/// </summary>
		public void StartSpawning() {
			if (IsSpawning)
				throw new UnityException(
					"A spawner should not be restarted before stopping it first");

			SpawnCount = 0;
			IsSpawning = true;
			StartCoroutine(Spawn());
		}

		/// <summary>
		/// Automatically start a <see cref="Spawn"/> coroutine after
		/// configuring the spawner.
		/// </summary>
		/// <param name="type">The type of object to spawn</param>
		/// <param name="limit">Number of objects to spawn</param>
		/// <param name="rate">
		/// The time in seconds to wait before each spawn
		/// </param>
		/// <param name="delay">
		/// The time in seconds to wait before first spawn
		/// </param>
		/// <param name="maxDistance">
		/// Max distance allowed to spawn away from player
		/// </param>
		/// <param name="minDistance">
		/// Min distance allowed to spawn away from player
		/// </param>
		/// <seealso cref="ObjectToSpawn"/>
		/// <seealso cref="SpawnLimit"/>
		/// <seealso cref="SpawnRate"/>
		/// <seealso cref="SpawnDelay"/>
		/// <seealso cref="MaxDistanceToSpawn"/>
		/// <seealso cref="MinDistanceToSpawn"/>
		public void StartSpawning(Type type, int limit, float rate,
					float delay, float maxDistance, float minDistance) {
			ObjectToSpawn = type;
			SpawnLimit = limit;
			SpawnRate = rate;
			InitialDelay = delay;
			MaxDistanceToSpawn = maxDistance;
			MinDistanceToSpawn = minDistance;
			StartSpawning();
		}

		public void StopSpawning() {
			if (!IsSpawning) return;
#if DEBUG
			Debug.Log("Spawn stopped");
#endif

			IsSpawning = false;
			StopCoroutine(Spawn());
		}

		public struct SpawnConfig
		{
			public readonly Type type;
			public readonly int limit;
			public readonly float rate, delay, maxDistance, minDistance;

			public SpawnConfig(
					Type type, int limit, float rate, float delay,
					float minDistance, float maxDistance) {
				this.type = type;
				this.limit = limit;
				this.rate = rate;
				this.delay = delay;
				this.maxDistance = maxDistance;
				this.minDistance = minDistance;
			}
		}

		public static Stack<SpawnConfig>[] ParseSpawnPattern(string spawnPatternFilePath) {
			Type type = default;
			int limit = default, multiplier;
			float rate = default, delay = default,
				  maxDistance = default, minDistance = default;

			bool doneParsingForCurrentLevel = false;

			var patterns = new Stack<SpawnConfig>();
			var groupsByType = new Stack<Stack<SpawnConfig>>();

			while (!doneParsingForCurrentLevel) {
				if (!(xmlPattern?.Read() ?? false)) {
					//TODO: Should we guard in case Resources.UnloadUnusedAssets
					//		dereferences spawnPatternFilePath?
					xmlPattern = XmlReader.Create(spawnPatternFilePath);
					xmlPattern.MoveToContent();
				}

				switch (xmlPattern.NodeType) {
				case XmlNodeType.Element:
					switch (xmlPattern.Name) {
					case "spawnable":
						if (!xmlPattern.HasAttributes) {
							throw new UnityException(
								"Spawnable type not specified in pattern.");
						}

						switch (xmlPattern.GetAttribute("type")) {
						case nameof(Enemies.Crasher):
							type = typeof(Enemies.Crasher);
							break;
						case nameof(Enemies.Drone):
							type = typeof(Enemies.Drone);
							break;
						case nameof(BulletCapsule):
							type = typeof(BulletCapsule);
							break;
						case nameof(HealthCapsule):
							type = typeof(HealthCapsule);
							break;
						case nameof(ArmorCapsule):
							type = typeof(ArmorCapsule);
							break;
						case nameof(PowerUpCapsule):
							type = typeof(PowerUpCapsule);
							break;
						}
						break;

					case "pattern":
						if (xmlPattern.HasAttributes)
							multiplier = int.Parse(xmlPattern.GetAttribute("repeat"));
						else
							multiplier = 1;
						break;

					// Get spawner configuration data.
					case nameof(limit):
						limit = xmlPattern.ReadElementContentAsInt();
						break;
					case nameof(rate):
						rate = xmlPattern.ReadElementContentAsFloat();
						break;
					case nameof(delay):
						delay = xmlPattern.ReadElementContentAsFloat();
						break;
					case nameof(maxDistance):
						maxDistance = xmlPattern.ReadElementContentAsFloat();
						break;
					case nameof(minDistance):
						minDistance = xmlPattern.ReadElementContentAsFloat();
						break;
					}
					break;

				case XmlNodeType.EndElement
				when xmlPattern.Name == "pattern":
					patterns.Push(
						new SpawnConfig(
							type, limit, rate, delay,
							minDistance, maxDistance
						)
					);
					break;

				case XmlNodeType.EndElement
				when xmlPattern.Name == nameof(Spawnable):
					groupsByType.Push(new Stack<SpawnConfig>(patterns));
					patterns.Clear();
					break;

				case XmlNodeType.EndElement
				when xmlPattern.Name == "level":
					doneParsingForCurrentLevel = true;
					break;
				}
			}
			return groupsByType.ToArray();
		}

		public static void SpawnerFactory(
				ICollection<Stack<SpawnConfig>> spawnPatterns,
				ref Dictionary<Type, List<Spawner>> spawners,
				ref Stack<Spawner> stashedSpawners) {

			/* A list to keep track of which types of spawners
			 * are not in the pattern and are not going to be
			 * used at all this turn. */
			Type[] types = new Type[spawners.Count];
			spawners.Keys.CopyTo(types, 0);
			List<Type> remainingSpawners = new List<Type>(types);

			while (spawnPatterns.Count > 0) {
				Stack<SpawnConfig> pattern = ((Stack<Stack<SpawnConfig>>)spawnPatterns).Pop();
				Type type = pattern.Peek().type;
				remainingSpawners.Remove(type);

				bool recursed = false;

				int spawnersRequired = pattern.Count;
				int spawnersAvailable = spawners[type].Count;

				// If there are not enough spawners available take from stash
				if (spawnersRequired > spawnersAvailable) {
					// How many spawners will be taken from stash?
					int spawnersReused;
					if (spawnersRequired <= spawnersAvailable + stashedSpawners.Count)
						spawnersReused = spawnersRequired - spawnersAvailable;
					else
						spawnersReused = stashedSpawners.Count;

					for (int i = 0; i < spawnersReused; i++) {
						spawners[type].Add(stashedSpawners.Pop());
						spawnersRequired--;
					}

					/* If there are still not enough spawners, continue to the
					 * rest of the patterns hoping that more spawners will be
					 * stashed in the meantime. */
					if (spawnersRequired > 0) {
						SpawnerFactory(spawnPatterns, ref spawners, ref stashedSpawners);
						recursed = true;

						// Take spawners from stash
						for (
							int i = stashedSpawners.Count;
							i != 0 && spawnersRequired > 0;
							i--
						) {
							spawners[type].Add(stashedSpawners.Pop());
							spawnersRequired--;
						}

						// If there are still not enough spawners, create new
						while (spawnersRequired-- > 0) {
							spawners[type].Add(Instantiate(
								Resources.Load<Spawner>("Prefabs/Spawner")));
						}
					}
				}
				else if (spawnersRequired < spawnersAvailable) {
					// Stash leftover spawners.

					int firstLeftover = spawnersRequired + 1,
						leftoversCount = spawnersAvailable - spawnersRequired;

					for (int i = firstLeftover; i < leftoversCount; i++)
						stashedSpawners.Push(spawners[type][i]);

					spawners[type].RemoveRange(firstLeftover, leftoversCount);
				}

				// Configure spawner using the retrieved data.
				foreach (Spawner spawner in spawners[type]) {
					spawner.Configure(pattern.Pop());
				}

				// Populating pools
				if (type == typeof(Crasher) && Spawnable.Pool<Crasher>.Count == 0) {
					Spawnable.Pool<Crasher>.Populate();
				}
				else if (type == typeof(Drone) && Spawnable.Pool<Drone>.Count == 0) {
					Spawnable.Pool<Drone>.Populate();

					if (Spawnable.Pool<EnemyBullet>.Count == 0)
						Spawnable.Pool<EnemyBullet>.Populate();
				}
				else if (type == typeof(ArmorCapsule) && Spawnable.Pool<ArmorCapsule>.Count == 0) {
					Spawnable.Pool<ArmorCapsule>.Populate();
				}
				else if (type == typeof(BulletCapsule) && Spawnable.Pool<BulletCapsule>.Count == 0) {
					Spawnable.Pool<BulletCapsule>.Populate();
				}
				else if (type == typeof(HealthCapsule) && Spawnable.Pool<HealthCapsule>.Count == 0) {
					Spawnable.Pool<HealthCapsule>.Populate();
				}
				else if (type == typeof(PowerUpCapsule) && Spawnable.Pool<PowerUpCapsule>.Count == 0) {
					Spawnable.Pool<PowerUpCapsule>.Populate();
				}

				/* If a recursion happened then the rest patterns have already
				 * been parsed, meaning that there is no need to continue the
				 * loop. */
				if (recursed) return;
			}

			/* Stash entire group of spawners when that type is not used
			 * this round. */
			foreach (var type in remainingSpawners) {
				if (spawners.ContainsKey(type)) {
					for (int i = 0; i < spawners[type].Count; i++)
						stashedSpawners.Push(spawners[type][i]);
					spawners.Remove(type);
				}
			}
		}
	}
}
