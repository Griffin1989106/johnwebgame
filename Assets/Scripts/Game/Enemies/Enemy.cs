﻿using UnityEngine;

namespace ShootAR.Enemies
{
	/// <summary>
	/// Parent class of all types of enemies.
	/// </summary>
	public abstract class Enemy : MonoBehaviour, ISpawnable, IOrbiter
	{
		/// <summary>
		/// The speed at which this object is moving.
		/// </summary>
		public float Speed { get; set; }
		/// <summary>
		/// The amount of points added to the player's score when destroyed.
		/// </summary>
		public int PointsValue { get; protected set; }
		/// <summary>
		/// The amount of damage the player recieves from this object's attack.
		/// </summary>
		[UnityEngine.Range(-Player.HEALTH_MAX, Player.HEALTH_MAX), UnityEngine.SerializeField]
		public int damage;
		public int Damage { get { return damage; } set { damage = value; } }
		/// <summary>
		/// Count of currently active enemies.
		/// </summary>
		public static int ActiveCount { get; set; }

		[SerializeField] protected AudioClip attackSfx;
		[SerializeField] protected GameObject explosion;
		protected AudioSource sfx;
		protected static GameManager gameManager;

		protected void Awake()
		{
			ActiveCount++;
		}

		protected virtual void Start()
		{
			//Create an audio source to play the audio clips
			sfx = gameObject.AddComponent<AudioSource>();
			sfx.clip = attackSfx;
			sfx.volume = 0.3f;
			sfx.playOnAwake = false;
			sfx.maxDistance = 10f;

			if (gameManager != null) gameManager = FindObjectOfType<GameManager>();
		}

		protected virtual void OnDestroy()
		{
			if (gameManager != null && !gameManager.GameOver)
			{
				gameManager.AddScore(PointsValue);
				Instantiate(explosion, transform.position, transform.rotation);
			}
			ActiveCount--;
		}

		/// <summary>
		/// Enemy moves towards a point using the physics engine.
		/// </summary>
		public void MoveTo(Vector3 point)
		{
			transform.LookAt(point);
			transform.forward = -transform.position;
			GetComponent<Rigidbody>().velocity = transform.forward * Speed;
		}

		public void MoveTo(float x, float y, float z)
		{
			Vector3 point = new Vector3(x, y, z);
			MoveTo(point);
		}

		/// <summary>
		/// Object orbits around a defined point by an angle based on its speed.
		/// </summary>
		/// <param name="orbit">The orbit to move in</param>
		public void OrbitAround(Orbit orbit)
		{
			transform.LookAt(orbit.direction, orbit.perpendicularAxis);
			transform.RotateAround(orbit.direction, orbit.perpendicularAxis, Speed * Time.deltaTime);
		}
	}
}