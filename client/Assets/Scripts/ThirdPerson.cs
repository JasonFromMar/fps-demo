﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShadowTransform {
	public Vector3 pos;
	public Quaternion rot;
};

public class ThirdPerson : MonoBehaviour {
	//configuration
	public float moving_turnspeed = 360f;
	public float turn_speed = 180f;
	public float speed_multiplier = 10f;
	public float player_ui_scale = 1.0f;
	public Slider player_hp;
	public Vector3 player_hp_offset;
	private AudioSource gunAudio;
	//component
	private Rigidbody RB;
	private Animator animator;
	private WeaponBase weapon = null;
	private GameObject lefthand;
	private GameObject righthand;

	private Vector3 sync_src_pos;
	private Quaternion sync_src_rot;
	/*
	private Vector3 sync_dst_pos;
	private Quaternion sync_dst_rot;
	*/
	private int uid;
	private ShadowTransform shadow = new ShadowTransform();
	private float sync_time;

	//camera
	Camera playercamera;


	public int Uid {
		get { return uid; }
		set { uid = value; }
	}

	public ShadowTransform Shadow {
		get { return shadow; }
	}

	void SwitchWeapon(string name) {
		if (weapon != null)
			weapon.Unload();
		var inst = Resources.Load("Prefabs/" + name, typeof(GameObject)) as GameObject;
		inst = Instantiate(inst) as GameObject;
		Debug.Assert(inst != null);
		weapon = inst.GetComponent<WeaponBase>();
		weapon.Equip(lefthand, righthand);
		Debug.Log("SwitWeapon");
	}

	void Start () {
		Debug.Log("ThirdPerson Start");
		RB = GetComponent<Rigidbody>();
		animator = GetComponent<Animator>();
		RB.constraints =
			RigidbodyConstraints.FreezeRotationX |
			RigidbodyConstraints.FreezeRotationY |
			RigidbodyConstraints.FreezeRotationZ;
		sync_src_pos = transform.position;
		sync_src_rot = transform.localRotation;
		shadow.pos = transform.position;
		shadow.rot = transform.localRotation;
		playercamera = CameraManager.main;
		gunAudio = GetComponent<AudioSource>();
		lefthand= Tool.FindChild(transform, "EthanLeftHandPinky1");
		righthand = Tool.FindChild(transform, "EthanRightHandPinky1");
		SwitchWeapon("shotgun_01");
	}

	void Update () {

	}

	void FixedAnimator() {
		//position
		var delta = transform.position;
		delta.y = 0;
		delta = shadow.pos - delta;
		var dir = transform.InverseTransformDirection(delta);
		float z = 0;
		float x = 0;
		if (dir.z > 0.1f)
			z = 0.5f;
		else if (dir.z < -0.1f)
			z = -0.5f;

		if (dir.x > 0.1f)
			x = 0.5f;
		else if (dir.x < -0.1f)
			x = -0.5f;
		var angleX = shadow.rot.eulerAngles.x;
		if (angleX > 180.0f)
			angleX -= 360.0f;
		animator.SetFloat("ForwardZ", z);
		animator.SetFloat("ForwardX", x);
		animator.SetFloat("ForwardY", angleX / 5.0f);
	}

	void FixedUI() {
		if (playercamera == null)
			return ;
		Vector3 pos = transform.position + player_hp_offset;
		float scale = player_ui_scale / Vector3.Distance(transform.position, playercamera.transform.position);
		pos = playercamera.WorldToScreenPoint(pos);
		if (pos.x > 0 && pos.y > 0 && pos.z > 0) {
			player_hp.gameObject.SetActive(true);
			player_hp.transform.position = pos;
			player_hp.transform.localScale = Vector3.one * scale;
		} else {
			player_hp.gameObject.SetActive(false);
		}
	}

	void FixedUpdate() {
		FixedAnimator();
		FixedUI();
	}

	public void Sync(Vector3 pos, Quaternion rot) {
		shadow.pos = pos;
		shadow.pos.y = 0;
		shadow.rot = rot;
		sync_time = Time.time;

		sync_src_pos = transform.position;
		sync_src_pos.y = 0;
		sync_src_rot = transform.localRotation;
		if (animator == null)
			return ;
		animator.speed = 1.0f;
	}

	public void SwapWeapon() {
		animator.SetTrigger("SwapWeapon");
	}

	public void Shoot(Vector3 point) {
		weapon.Shoot(point);
	}

	public void Jump() {
		animator.SetTrigger("Jump");
		RB.velocity = Vector3.up * 10;
	}

	public void OnAnimatorMove()
	{
		if (Time.deltaTime > 0.0f) {
			var pos = Vector3.Slerp(transform.position, shadow.pos, 0.1f);
			pos.y = transform.position.y;
			transform.position = pos;
			Quaternion dst = Quaternion.Euler(0.0f, shadow.rot.eulerAngles.y, 0.0f);
			transform.localRotation = Quaternion.Slerp(sync_src_rot, dst, (Time.time - sync_time) / 0.1f);
		}
	}
}
