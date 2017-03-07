using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyOnStart : MonoBehaviour {

	// Use this for initialization
	void Start () {
		Destroy(this.gameObject);
	}
}
