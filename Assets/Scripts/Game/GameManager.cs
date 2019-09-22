﻿using ShootAR.Enemies;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShootAR
{

	public class GameManager : MonoBehaviour
	{
		[SerializeField] private AudioClip victoryMusic;
		private Dictionary<Type, Spawner> spawner;
		[SerializeField] private ScoreManager scoreManager;
		[Obsolete] private bool exitTap;    // Why do we need this? Should it be removed?
		private AudioSource audioPlayer;
		[SerializeField] private GameState gameState;
		[SerializeField] private Button fireButton;
		[SerializeField] private UIManager ui;
		private WebCamTexture cam;
		[SerializeField] private RawImage backgroundTexture;
		[SerializeField] private Player player;
		[SerializeField] private Bullet bulletPrefab;
		private const int CAPSULE_BONUS_POINTS = 50;

		public static GameManager Create(
				Player player, GameState gameState,
				ScoreManager scoreManager = null,
				AudioClip victoryMusic = null, AudioSource sfx = null,
				Button fireButton = null, RawImage background = null,
				UIManager ui = null
			) {
			var o = new GameObject(nameof(GameManager)).AddComponent<GameManager>();

			o.player = player;
			o.gameState = gameState;
			o.scoreManager = scoreManager;
			o.victoryMusic = victoryMusic;
			o.audioPlayer = sfx;
			o.fireButton = fireButton;
			o.backgroundTexture = background ?? new GameObject("Background")
														.AddComponent<RawImage>();
			o.ui = ui ??
				UIManager.Create(
					uiCanvas: new GameObject(),
					pauseCanvas: new GameObject(),
					bulletCount: new GameObject().AddComponent<Text>(),
					messageOnScreen: new GameObject().AddComponent<Text>(),
					score: new GameObject().AddComponent<Text>(),
					roundIndex: new GameObject().AddComponent<Text>(),
					sfx: null, pauseSfx: null, gameState: o.gameState
				);

			return o;
		}

		private void Awake() {
#if UNITY_ANDROID
			if (!SystemInfo.supportsGyroscope) {
				exitTap = true;
				const string error = "This device does not have Gyroscope";
				if (ui != null)
					ui.MessageOnScreen.text = error;
				throw new UnityException(error);
			}
			else {
				Input.gyro.enabled = true;
			}

			//Set up the rear camera
			for (int i = 0; i < WebCamTexture.devices.Length; i++) {
				if (!WebCamTexture.devices[i].isFrontFacing) {
					cam = new WebCamTexture(WebCamTexture.devices[i].name, Screen.width, Screen.height);
					break;
				}
			}
#endif
			/* Do not use elif here. While testing
			 * using Unity Remote 5, it does not use
			 * the camera on the phone and it has to
			 * fall back on the webcam. We need both
			 * UNITY_ANDROID and UNITY_EDITOR for that. */
#if UNITY_EDITOR
			cam = new WebCamTexture();
#endif
		}

		private void Start() {
			if (player == null)
				throw new UnityException("Player object not found");
			if (gameState == null)
				throw new UnityException("GameState object not found");
			if (cam == null) {
				const string error = "This device does not have a rear camera";
				ui.MessageOnScreen.text = error;
				throw new UnityException(error);
			}

			cam.Play();
			backgroundTexture.texture = cam;
			backgroundTexture.rectTransform
				.localEulerAngles = new Vector3(0, 0, cam.videoRotationAngle);
			float scaleY = cam.videoVerticallyMirrored ? -1.0f : 1.0f;
			float videoRatio = (float)cam.width / (float)cam.height;
			backgroundTexture.rectTransform.localScale = new Vector3(scaleY, scaleY / videoRatio, 1);  //Through testing i found out that using these settings makes the most optimal outcome

			/*
#if UNITY_ANDROID
            backgroundTexture.rectTransform.localScale = new Vector3(Screen.width, Screen.height, 0);    //Didnt try this, but got the inspiration from it
#endif
            */
			fireButton?
				.onClick.AddListener(() => {
					if (gameState.GameOver) {
						/* Because pools are static they require to be manually
						 * emptied when the scene is reloaded or else bugs will
						 * occur. Not all pools are required to be emptied, but
						 * this way it is easier to manage.*/
						Spawnable.Pool<Capsule>.Empty();
						Spawnable.Pool<Crasher>.Empty();
						Spawnable.Pool<Drone>.Empty();
						Spawnable.Pool<Bullet>.Empty();

						SceneManager.LoadScene(1);
					}
					else if (gameState.RoundWon) {
						ui.MessageOnScreen.text = "";
						AdvanceLevel();
					}
					else
						player.Shoot();
				});

			audioPlayer = gameObject.AddComponent<AudioSource>();
			ui.BulletCount.text = player.Ammo.ToString();

			spawner = new Dictionary<Type, Spawner>();
			Spawner[] spawners = FindObjectsOfType<Spawner>();
			if (spawners == null) {
				throw new UnityException("Could not find spawners.");
			}
			else {
				foreach (Spawner s in spawners) {
					Type type = s.ObjectToSpawn.GetType();
					spawner.Add(type, s);
#if DEBUG
					Debug.Log($"Found spawner of type \"{type}\"");
#endif
				}
			}

			/* The round index is assigned an initial value diminished by 1,
			 * since AdvanceLevel will add it back. */
			gameState.Level = Configuration.StartingLevel - 1;
			player.Ammo += gameState.Level * 15;    /* initial Ammo value set in
													 * Inspector */
			Spawnable.Pool<Bullet>.Populate(bulletPrefab, 10);
			AdvanceLevel();

			GC.Collect();
		}

		private void OnEnable() {
			gameState.OnGameOver += OnGameOver;
			gameState.OnRoundWon += OnRoundWon;
		}

		private void FixedUpdate() {
			if (!gameState.GameOver) {
				// Round Won
				bool spawnersStoped = true;
				foreach (var type in spawner.Keys) {
					if (type.IsSubclassOf(typeof(Enemy))
						&& spawner[type].IsSpawning) {
						spawnersStoped = false;
						break;
					}
				}
				if (spawnersStoped && Enemy.ActiveCount == 0) {
					gameState.RoundWon = true;
				}

				// Defeat
				else if (Enemy.ActiveCount > 0 && Bullet.ActiveCount == 0
						&& player.Ammo == 0) {
					gameState.GameOver = true;
				}
			}
		}

		private void OnDisable() {
			if (gameState != null) {
				gameState.OnGameOver -= OnGameOver;
				gameState.OnRoundWon -= OnRoundWon;
			}
		}

		private void OnDestroy() {
			/* cam.Stop() is required to stop the camera so it can be
			 * restarted when the scene loads again; else, after the 
			 * scene reloads, the feedback will be blank. */
			cam.Stop();
			gameState.GameOver = true;
			ClearScene();
		}

		private void OnApplicationQuit() {
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#endif
		}


		/// <summary>
		/// In here lies the code that runs before each round.
		/// </summary>
		private void AdvanceLevel() {
			gameState.Level++;
#if DEBUG
			Debug.Log($"Advancing to level {gameState.Level}");
#endif

			foreach (var s in spawner) {
				#region Spawn Patterns
				if (s.Key == typeof(Crasher)) {
					s.Value.StartSpawning(
						limit: 4 * gameState.Level + 8,
						rate: 3f - gameState.Level * .1f,
						delay: 3f);

					if (Spawnable.Pool<Crasher>.Count == 0)
						Spawnable.Pool<Crasher>
								.Populate((Crasher)s.Value.ObjectToSpawn);
				}
				else if (s.Key == typeof(Drone)) {
					s.Value.StartSpawning(
						limit: 3 * gameState.Level + 6,
						rate: 3f - gameState.Level * .1f,
						delay: 4f);

					if (Spawnable.Pool<Drone>.Count == 0)
						Spawnable.Pool<Drone>
								.Populate((Drone)s.Value.ObjectToSpawn);
					if (Spawnable.Pool<EnemyBullet>.Count == 0)
						Spawnable.Pool<EnemyBullet>
								.Populate(((Drone)s.Value.ObjectToSpawn).Bullet);
				}
				else if (s.Key == typeof(Capsule)) {
					s.Value.StartSpawning(
						limit: gameState.Level + 2,
						rate: 3f + gameState.Level * .5f,
						delay: 10f);

					if (Spawnable.Pool<Capsule>.Count == 0)
						Spawnable.Pool<Capsule>
								.Populate((Capsule)s.Value.ObjectToSpawn);
				}
				else throw new Exception($"Unrecognised type of spawner: {s.Key}");
				#endregion

				int ammoEnemyDifference = player.Ammo - s.Value.SpawnLimit;
				int ammoReward = 6;
				if (ammoEnemyDifference > 0)
					scoreManager.AddScore(ammoEnemyDifference * 10);
				else if (ammoEnemyDifference < 0)
					ammoReward += -ammoEnemyDifference;

				player.Ammo += ammoReward;	// use sum of ammo rewards to avoid
											// multiple GUI updates
			}

			gameState.RoundWon = false;
		}

		/// <summary>
		/// Destroys all spawned objects. 
		/// </summary>
		private void ClearScene() {
#if DEBUG
			Debug.Log("Clearing scene...");
#endif

			// Be merciful. Player deserves some points for the unused capsules.
			if (gameState.RoundWon) {
				Capsule[] capsules = FindObjectsOfType<Capsule>();
				scoreManager?.AddScore(capsules.Length * CAPSULE_BONUS_POINTS);
				foreach (var c in capsules) c.Destroy();
			}

			Spawnable[] spawnables = FindObjectsOfType<Spawnable>();
			foreach (var s in spawnables) s.Destroy();

#if DEBUG
			Debug.Log("Scene cleared.");
#endif
		}

		public void GoToMenu() {
			cam.Stop();
			SceneManager.LoadScene("MainMenu");
		}

		private void OnGameOver() {
			Debug.Log("Player defeated");
			if (ui != null) {
				var survivedRounds = gameState.Level - Configuration.StartingLevel;
				ui.MessageOnScreen.text =
					$"Game Over\n\n" +
					$"Rounds Survived : {survivedRounds}";
			}

			ClearScene();
		}

		private void OnRoundWon() {
			Debug.Log("Round won");
			ui.MessageOnScreen.text = "Round Clear!";
			audioPlayer?.PlayOneShot(victoryMusic, 0.7f);
			ClearScene();
		}


#if DEBUG
		private void OnGUI() {
			GUILayout.Label(
				$"Build {Application.version}\n" +
				$"Game Over: {gameState.GameOver}\n" +
				$"Round Over: {gameState.RoundWon}"
			);
		}
#endif
	}
}
