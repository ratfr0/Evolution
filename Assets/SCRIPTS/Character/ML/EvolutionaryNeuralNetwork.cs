﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EvolutionaryNeuralNetwork {

	private const int INPUTLAYERS = 11;
	private const int HIDDENLAYERS = 6;
	private const int OUTPUTLAYERS = 3;
	private const float HUE_SEPARATION = 0.075f;

	private float [,,] W_1;
	private float [,] B_1;

	private float [,,] W_2;
	private float [,] B_2;
	private float [] lr;

	private struct WinnerDistance {
		public WinnerDistance (float distance, int index) {
			this.distance = distance;
			this.index = index;
		}
		public float distance;
		public int index;
	}
	private WinnerDistance [] winners = new WinnerDistance [3];

	public UnityEngine.Color [] enemyColors;
	private ControlCharacterML [] enemies;
	private Character player;
	private int winner_2_idx;
	private int winner_1_idx;
	private int winner_3_idx;

	public EvolutionaryNeuralNetwork (Character player, ControlCharacterML [] enemies) {
		ResetWinners ();
		this.player = player;
		this.enemies = enemies;
		enemyColors = new Color [num_enemies];
		W_1 = new float [num_enemies, HIDDENLAYERS, INPUTLAYERS]; //12 inputs, your momentum vector, their momentum vector, the x y z distances
		B_1 = new float [num_enemies, HIDDENLAYERS];
		W_2 = new float [num_enemies, OUTPUTLAYERS, HIDDENLAYERS];
		B_2 = new float [num_enemies, OUTPUTLAYERS];
		lr = new float [num_enemies];
		for (int i = 0; i < num_enemies; i++) {
			lr [i] = 0.01f;
			enemyColors [i] = new ColorHSV (i * 1f / num_enemies, 1f, 1f, 1f);
			enemies [i].character.color = enemyColors [i];
		}
		for (int i = 0; i < num_enemies; i++) {
			for (int j = 0; j < HIDDENLAYERS; j++) {
				for (int k = 0; k < INPUTLAYERS; k++) {
					W_1 [i, j, k] = 2.0f * (UnityEngine.Random.value - .5f) * 0.01f;
				}
				B_1 [i, j] = 2.0f * (UnityEngine.Random.value - .5f) * 0.01f;
			}
		}

		for (int i = 0; i < num_enemies; i++) {
			for (int j = 0; j < OUTPUTLAYERS; j++) {
				for (int k = 0; k < HIDDENLAYERS; k++) {
					W_2 [i, j, k] = 0.001f * (UnityEngine.Random.value - .5f);
				}
				B_2 [i, j] = -0.1f;//2.0f * (UnityEngine.Random.value - .5f);
				if(j==1) B_2 [i, j] = 0.1f;
			}
		}

	}

	private void ResetWinners () {
		for (int x = 0; x < winners.Length; x++) {
			winners [x] = new WinnerDistance (Mathf.Infinity, x);
		}
	}

	// Calculates info from player
	public void Update () {
		float [,] data = new float [num_enemies, INPUTLAYERS];
		for (int x = 0; x < num_enemies; x++) {
			float playerDistance = Vector2.Distance (enemies [x].character.transform.position, player.transform.position);
			if (enemies [x].character.dead) {
				playerDistance = playerDistance * 100000f;
				data [x, 0] = (enemies [x].character.transform.position.x - player.transform.position.x) * 1000f;
				data [x, 1] = (enemies [x].character.transform.position.y - player.transform.position.y) * 1000f;
			}
			else {
				data [x, 0] = enemies [x].character.transform.position.x - player.transform.position.x;
				data [x, 1] = enemies [x].character.transform.position.y - player.transform.position.y;
			}
			//playerDistance = Vector2.Distance (enemies [x].character.transform.position, player.transform.position);
			if (x < 3) {
				winners [x] = new WinnerDistance (playerDistance, x);
			}
			else if (playerDistance < winners [0].distance) {
				winners [2] = winners [1];
				winners [1] = winners [0];
				winners [0] = new WinnerDistance (playerDistance, x);
			}
			else if (playerDistance < winners [1].distance) {
				winners [2] = winners [1];
				winners [1] = new WinnerDistance (playerDistance, x);
			}
			else if (playerDistance < winners [2].distance) {
				winners [2] = new WinnerDistance (playerDistance, x);
			}
			data [x, 2] = enemies [x].character.velocity.x;
			data [x, 3] = enemies [x].character.velocity.y;
			data [x, 4] = player.velocity.x;
			data [x, 5] = player.velocity.y;
			data [x, 6] = enemies [x].character.transform.position.x;
			data [x, 7] = enemies [x].character.transform.position.y;
			data [x, 8] = enemies [x].character.timeSinceLastJump;
			data [x, 9] = enemies [x].character.grounded ? 1f : 0f;
			data [x, 10] = Mathf.NegativeInfinity;
			for (int y = 0; y < 100; y++) {
				if (x != y) {
					float neighborDistance = Vector2.Distance (enemies [x].character.transform.position, enemies [y].character.transform.position);
					if (neighborDistance > data [x, 10]) {
						data [x, 10] = neighborDistance;
					}
				}
			}
		}
		UpdateControllers (GetMoves (data));
	}

	/// <summary>
	/// INPUT
	/// First dimension: for each entity
	/// Second dimension: xToPlayer, yToPlayer, myXVel, myYVel, playerXVel, playerYVel
	/// OUTPUT
	/// 0: jump?
	/// 1: move left
	/// 2: move right
	/// </summary>
	bool [,] GetMoves (float [,] inp) {
		bool [,] output = new bool [num_enemies, 3];
		for (int k = 0; k < num_enemies; k++) {
			float [] a = new float [HIDDENLAYERS];
			for (int i = 0; i < HIDDENLAYERS; i++) {
				float temp = 0f;
				for (int j = 0; j < INPUTLAYERS; j++) {
					temp += W_1 [k, i, j] * inp [k, j];
				}
				a [i] = temp + B_1 [k, i];
				if (a [i] < 0) a [i] = 0;//RELU
			}
			float b;
			for (int i = 0; i < OUTPUTLAYERS; i++) {
				float temp = 0.0f;
				for (int j = 0; j < HIDDENLAYERS; j++) {
					temp += W_2 [k, i, j] * a [j];
				}
				b = temp + B_2 [k, i];
				if (b < 0) { output [k, i] = false; }
				else {
					output [k, i] = true;//SIGMOID}
				}
			}
		}

		//a = W_1 [k,,] * inp[k,] + B_1[k,]
		//b = ReLU(a)
		//c = W_2 [k,,] * b + B_2[k,]
		//out = sigmoid(c)

		return output;

	}

	/// 0: jump?
	/// 1: move left
	/// 2: move right
	void UpdateControllers (bool [,] data) {
		for (int x = 0; x < num_enemies; x++) {
			int moveDirection = 0;
			if (data [x, 1]) {
				moveDirection--;
			}
			if (data [x, 2]) {
				moveDirection++;
			}
			enemies [x].UpdateActions (new FrameAction (moveDirection, data [x, 0]));
		}
	}

	/// <summary>
	/// call then respawn
	/// </summary>
	void UpdateWeightAndRespawn (int winner_1_idx, int winner_2_idx, int winner_3_idx) {//
		this.winner_1_idx = winner_1_idx;
		this.winner_2_idx = winner_2_idx;
		this.winner_3_idx = winner_3_idx;

		weight_update_for_loop (winner_1_idx, 0, num_enemies / 2, 0f);
		weight_update_for_loop (winner_2_idx, num_enemies / 2, 5 * num_enemies / 6, 1f / 3f);
		weight_update_for_loop (winner_3_idx, 5 * num_enemies / 6, num_enemies, 2f / 3f);
	}

	private void weight_update_for_loop (int winner_i, int lower_bound, int upper_bound, float hueOffset) {
		for (int i = lower_bound; i < upper_bound; i++) {
			if (i != winner_1_idx && i != winner_2_idx && i != winner_3_idx) {
				lr [i] = lr [winner_i] + 2.0f * (UnityEngine.Random.value - .5f) / 20.0f;
				ColorHSV tempColor = enemyColors [winner_i];
				tempColor.h = (tempColor.h + Utility.randomPlusOrMinusOne * HUE_SEPARATION + hueOffset).Normalized01 ();
				enemyColors [i] = tempColor;
				enemies [i].character.color = enemyColors [i];
				enemies [i].character.RespawnAtPosition (enemies [winner_i].character.transform.position + (Vector3)Random.insideUnitCircle);

				for (int j = 0; j < HIDDENLAYERS; j++) {
					for (int k = 0; k < INPUTLAYERS; k++) W_1 [i, j, k] = W_1 [winner_i, j, k] + lr [i] * 2.0f * (UnityEngine.Random.value - .5f);
					B_1 [i, j] = B_1 [winner_i, j] + lr [i] * 2.0f * (UnityEngine.Random.value - .5f);
				}

				for (int j = 0; j < OUTPUTLAYERS; j++) {
					for (int k = 0; k < HIDDENLAYERS; k++) W_2 [i, j, k] = W_2 [winner_i, j, k] + lr [i] * 2.0f * (UnityEngine.Random.value - .5f);
					B_2 [i, j] = B_2 [winner_i, j] + lr [i] * 2.0f * (UnityEngine.Random.value - .5f);
				}
			}
		}
	}

	private int num_enemies {
		get {
			return NeuralNetController.staticRef.numberOfEnemies;
		}
	}

	public void KillAndRespawn () {
		NeuralNetController.staticRef.GrindMusic ();
		SoundCatalog.PlayGenerationSound ();
		Scoreboard.AddGeneration ();
		UpdateWeightAndRespawn (winners [0].index, winners [1].index, winners [2].index);
		ResetWinners ();
	}
}