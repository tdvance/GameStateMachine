using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System;

/// <summary>
/// A state machine for overall game state.  It is a Singleton class.
/// 
/// Rather than provide an "instance" accessor, I just piggyback on the FindObjectOfType 
/// method of gameobjects.  Less room for introducing errors this way.
/// </summary>
public class StateMachine : MonoBehaviour {

    /// <summary>
    /// These are events triggered by states: when entering, exiting, or loading the 
    /// primary scene associated with a state, if any.
    /// </summary>
    [Serializable]
    public class StateAction : UnityEvent {
    }

    /// <summary>
    /// The states are of this class.  States must have unique names because they 
    /// are accessed by name.
    /// </summary>
    [Serializable]
    public class State {
        [Tooltip("Name of the State")]
        public string name;

        [Tooltip("If positive, will automatically transition to another state in this many seconds")]
        public float autoTransitionTime = 0;

        [Tooltip("Which state to automatically transition to, if autoTransitionTime is positive")]
        public int whichAutoTransition = 0;

        [Tooltip("Load the specified scene automatically on entering this state.  Blank or 'none' or 'None' to not load any scene.")]
        public string loadThisScene = "None";

        [Tooltip("Numbered list of states this state can transition to")]
        public string[] canTransitionTo = new string[] { "Error", "Error", "Error" };

        [Tooltip("Do this upon entering the state")]
        public StateAction onEnter;

        [Tooltip("Do this just before the state transitions to another state")]
        public StateAction beforeExit;

        [Tooltip("Do this when the state's scene is loaded.  If no scene to be loaded, does this right after 'onEnter' actions.")]
        public StateAction onSceneLoaded;

        //constructor for convenience: needed only to create a default "Start" state.    
        public State(string name) {
            this.name = name;
        }
    }

    //the name of the start state, what the machine is in upon awakening
    private static string startState = "Start";

    [Tooltip("Changing this changes the state the machine is in")]
    public string currentState = startState;

    [Tooltip("All the available states")]
    public State[] states = { new State(startState) };

    //history of states; allows using PreviousState() to go back to previous state
    Stack<string> history = new Stack<string>();

    //the currently active state
    State activeState;

    /// <summary>
    /// Return the currently active state
    /// </summary>
    /// <returns>State machine's current state</returns>
    public State GetActiveState() {
        return activeState;
    }

    /// <summary>
    /// Go back to the state the machine was in before the current state.  Does nothing if 
    /// state history is empty.
    /// </summary>
    public void PreviousState() {
        while (history.Count > 0 && history.Peek() == currentState) {
            history.Pop();
        }
        if (history.Count > 0) {
            currentState = history.Pop();
        }
    }

    /// <summary>
    /// Do a state transition to the one indexed by "which".  Uses "canTransitionTo" 
    /// field of the current State.
    /// </summary>
    /// <param name="which"></param>
    public void Transition(int which) {
        if (activeState == null) {
            Debug.LogError("No Transition possible");
        } else if (which < 0 || which >= activeState.canTransitionTo.Length) {
            Debug.LogError("Transition number " + which + " out of range (0..." + (activeState.canTransitionTo.Length - 1) + ")");
        } else {
            currentState = activeState.canTransitionTo[which];
        }
    }

    //make this a singleton
    private void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    //set up state machine in Start state
    private void Start() {
        history.Push(currentState);
        State state = FindCurrentState();
        EnterState(state);
    }

    //see if "currentState" changed and transition if it did
    private void Update() {
        CheckTransition();
    }

    //Load specified scene for the state in the background
    IEnumerator SetUpScene() {
        if (activeState.loadThisScene.Length > 0 && activeState.loadThisScene.ToLower() != "none") {
            AsyncOperation op = SceneManager.LoadSceneAsync(activeState.loadThisScene);
            op.allowSceneActivation = true;
            while (!op.isDone) {
                yield return null;
            }
        }
        activeState.onSceneLoaded.Invoke();
        //start autotransition countdown after scene is loaded
        if (activeState.autoTransitionTime > 0) {
            Invoke("AutoTransition", activeState.autoTransitionTime);
        }
        yield return null;
    }

    //called automatically if autoTransitionTime is positive to change state automatically
    void AutoTransition() {
        Transition(activeState.whichAutoTransition);
    }

    //see if "currentState" changed and transition if it did
    void CheckTransition() {
        if (activeState.name != currentState) {
            CancelInvoke();// stop any pending actions for this state
            ExitState();//leave the state
            history.Push(currentState);//put new state in history
            State state = FindCurrentState();//find the State having correct name
            EnterState(state);//Enter the state
        }
    }

    //Find the State object having the name of the currentState
    State FindCurrentState() {
        foreach (State state in states) {
            if (state.name == currentState) {
                return state;
            }
        }
        return null;
    }

    //Enter a state, setting up scene and invoking actions
    void EnterState(State state) {
        activeState = state;
        activeState.onEnter.Invoke();
        StartCoroutine("SetUpScene");
    }

    //Exit a state, invoking actions
    void ExitState() {
        activeState.beforeExit.Invoke();
    }

}
