using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// Maintain the current state that the state machine is in
/// </summary>
public class SimpleStateMachine : MonoBehaviour {

    #region Public interface

    /// <summary>
    /// A state in the state machine
    /// </summary>
    [Serializable]
    public class State {
        [Tooltip("Name of the State")]
        public string name;

        [Tooltip("List of states this state can transition to")]
        public string[] canTransitionTo;

        //constructor for convenience: needed only to create a default "Start" state.    
        public State(string name) {
            this.name = name;
        }
    }

    [Tooltip("Changing this changes the state the machine is in")]
    public string currentState = startState;

    [Tooltip("All the available states")]
    public State[] states = { new State(startState) };

    public void Transition(int which) {
        currentState = activeState.canTransitionTo[which];
    }
    #endregion


    //the name of the start state, what the machine is in upon awakening
    private static string startState = "Start";

    //the currently active state
    State activeState;

    // Use this for initialization
    void Start() {
        //set the current state
        activeState = FindState(currentState);
    }

    // Update is called once per frame
    void Update() {
        if (currentState != activeState.name) {
            //if state changes, reset the current state
            activeState = FindState(currentState);
        }
    }

    //find state having given name
    State FindState(string name) {
        foreach (State state in states) {
            if (state.name == name) {
                return state;
            }
        }
        Debug.LogError("Missing state: " + name);
        return null;
    }
}
