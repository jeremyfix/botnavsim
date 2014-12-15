﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// This is a manager object used to overlook the running of a simulation.
public class Simulation : MonoBehaviour {

	public enum State {
		preSimulation,
		simulating,
		stopped,
		finished
	}
	
	[System.Serializable]
	public class Settings {
		public string title = "New Simulation";
		public string date = System.DateTime.Now.ToShortDateString();
		public string time = System.DateTime.Now.ToShortTimeString();
		public string environmentName = "<none>";
		public string navigationAssemblyName = "<none>";
		public string robotName = "<none>";
		public int numberOfTests = 1;
		public bool randomizeDestination = false;
		public bool randomizeOrigin = false;
		public bool continueOnNavObjectiveComplete = false;
		public bool continueOnRobotIsStuck = false;
		public float initialTimeScale = 1f;
		public string name {
			get {
				return robotName + "|" + navigationAssemblyName + "|" + environmentName;
			}
		}
		public string summary {
			get {
				string s = "";
				s += "Number of tests: " + numberOfTests;
				s += "\nRobot: " + robotName;
				s += "\nNavigation Assembly: " + navigationAssemblyName;
				if (randomizeDestination)
					s += "\nRandom destination";
				if (randomizeOrigin)
					s += "\nRandom origin.";
				if (continueOnRobotIsStuck)
					s += "\nAuto repeat if robot gets stuck.";
				
				return s;
			}
		}
	}
	
	// Singleton pattern
	public static Simulation Instance;
	
	
	/** Static  Properties **/
	
	
	// Settings for the current simulation (as specified by UI_setup)
	public static Settings settings = new Settings();

	// List of settings to iterate through in batch mode
	public static List<Settings> batch = new List<Settings>();
	
	// Simulation state (enumeration)
	public static State state {get; set;}
	
	// Current test number (1 to settings.numberOfTests)
	public static int testNumber {get; set;}
	
	// Reference to the robot monobehaviour
	public static Robot robot {
		get { return _robot; }
		set {
			if(_robot) _robot.transform.Recycle();
			_robot = value;
			robot.destination = destination.transform;
		}
	}
	
	// Reference to the environment
	public static GameObject environment {
		get; set;
	}
	
	// Reference to the destination
	public static GameObject destination { get; set; }
	
	// Simulation states
	public static bool preSimulation {
		get { return state == State.preSimulation; }
	}
	public static bool isRunning {
		get { return state == State.simulating; }
	}
	public static bool isStopped {
		get { return state == State.stopped; }
	}
	public static bool isFinished {
		get { return state == State.finished; }
	}
	
	// Simulation bounds (search space for INavigation)
	public static Bounds bounds;
	
	// is the simulation ready to begin?
	public static bool isReady {
		get {
			return settings.robotName != "<none>" && settings.navigationAssemblyName != "<none>";
		}
	}
	
	// is the simulation paused?
	public static bool paused {
		get { return _paused; }
		set {
			_paused = value;
			if (_paused) Time.timeScale = 0f;
			else Time.timeScale = timeScale;
		}
	}

	// Time (seconds) since robot started searching for destination.
	public static float time {
		get {
			if (isRunning) _stopTime = Time.time;
			return _stopTime - _startTime;
		}
	}
	
	// Simulation time scale 
	public static float timeScale {
		get { return _timeScale; }
		set {
			_timeScale = value;
			Time.timeScale = value;
		}
	}

	// Time variables used to calculate Simulation.time
	private static float _startTime;
	private static float _stopTime;
	
	private static Robot _robot;
	private static bool _paused;
	private static float _timeScale;
	
	/** Static Methods **/
	
	// Start the simulation 
	public static void Begin() {
		if (environment) environment.transform.Recycle();
		environment = EnvLoader.LoadEnvironment(settings.environmentName);
		SetBounds();
		destination.transform.position = RandomInBounds();
		Camera.main.transform.parent = null;
		robot = BotLoader.LoadRobot(settings.robotName);
		
		robot.navigation = NavLoader.LoadPlugin(settings.navigationAssemblyName);

		timeScale = settings.initialTimeScale;
		testNumber = 0;
		NextTest();
	}
	
	// Skip to the next test in the simulation
	public static void NextTest() {
		if (++testNumber >= settings.numberOfTests) {
			End();
			return;
		}
		Instance.StartCoroutine(StartTestRoutine());
	}
	
	public static void StopTest() {
		if (robot) {
			robot.rigidbody.velocity = Vector3.zero;
			robot.moveEnabled = false;
		}
		state = State.stopped;
	}
	
	// Stop the simulation
	public static void End() {
		if (robot) {
			robot.rigidbody.velocity = Vector3.zero;
			robot.moveEnabled = false;
		}
		state = State.finished;
	}
	
	// Routine for starting a new test
	private static IEnumerator StartTestRoutine() {
		StopTest();
		yield return new WaitForSeconds(1f);
		if (settings.randomizeOrigin)
			robot.transform.position = RandomInBounds();
		if (settings.randomizeDestination)
			destination.transform.position = RandomInBounds();
		yield return new WaitForSeconds(1f);
		robot.moveEnabled = true;
		robot.NavigateToDestination();
		state = State.simulating;
		_startTime = Time.time;
	}
	
	// Set the simulation bounds to encapsulate all renderers in scene
	private static void SetBounds() {
		bounds = new Bounds(Vector3.zero, Vector3.zero);
		foreach(Renderer r in FindObjectsOfType<Renderer>())
			bounds.Encapsulate(r.bounds);
	}
	
	// Return a random position inside the simulation bounds
	private static Vector3 RandomInBounds() {
		Vector3 v = bounds.min;
		v.x += Random.Range(0f, bounds.max.x);
		v.y += Random.Range(0f, bounds.max.y);
		v.z += Random.Range(0f, bounds.max.z);
		return v;
	}
	
	/** Instance Members **/
	
	public AstarNative astar;
	private bool _hideMenu;

	/** Instance Methods **/

	void Awake() {
		if (Instance) {
			Destroy(this.gameObject);
		}
		else {
			Instance = this;
		}
		astar = GetComponent<AstarNative>();

	}
	
	void Start() {
		destination = GameObject.Find("Destination");
	}
	
	void Update() {
		if (isRunning) {
			// check for conditions to end the test
			if (robot.atDestination && settings.continueOnNavObjectiveComplete) {
				NextTest();
			}
			if (robot.isStuck && settings.continueOnRobotIsStuck) {
				NextTest();
			}
			

		}
	}


			
	void OnDrawGizmos() {
		Gizmos.DrawWireCube(bounds.center, bounds.size);
	}
}
