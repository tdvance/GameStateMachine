using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// The game state machine controller singleton
/// </summary>
public class GameStateMachine : MonoBehaviour {

    /// <summary>
    /// A state in the machine
    /// </summary>
    public class GameState {
        public string name;
        public string scene;
        public bool isAdditive;
        public GameState timedTransition;
        public float time;
        public Dictionary<GameEvent, GameState> eventTransitions;
        public HashSet<GameEvent> backTransitionEvents;
    }

    /// <summary>
    /// An event
    /// </summary>
    public class GameEvent {
        public string name;
    }

    /// <summary>
    /// properties that can be taken as the "compiled" state machine data
    /// </summary>
    ParseSM parser;
    Dictionary<string, GameState> states;
    Dictionary<string, GameEvent> events;
    GameState currentState;
    GameState errorState;


    /// <summary>
    /// Properties that keep track of the running state machine
    /// </summary>
    string unloadInsteadOfLoadNew = null;

    HashSet<GameEvent> registeredEvents = new HashSet<GameEvent>();
    Queue<GameEvent> eventsPending = new Queue<GameEvent>();

    Stack<GameState> history = new Stack<GameState>();


    //show the current state machine information
    [TextArea(3, 20)]
    public string message;

    /// <summary>
    /// Send an event to the state machine
    /// </summary>
    /// <param name="name">Event name, not case sensitive</param>
    /// <returns>true if event received by current state, false if dropped (usually means state doesn't register that event)</returns>
    public bool SendEvent(string name) {
        GameEvent e = events[name.ToLower()];
        if (registeredEvents.Contains(e)) {
            eventsPending.Enqueue(e);
            return true;
        } else {
            Debug.LogWarning("Event " + name + " not handled by current state");
            return false;
        }
    }

    /// <summary>
    /// Make it a Singleton
    /// </summary>
    void Awake() {
        DontDestroyOnLoad(gameObject);
    }

    // Use this for initialization
    void Start() {
        parser = FindObjectOfType<ParseSM>();
        if (!parser.programIsValid) {
            Debug.LogError("No valid state machine program found");
        } else {
            LoadStateMachine();
            ShowStateMachine();// show state machine summary in the Inspector
        }
    }

    /// <summary>
    /// Load the state machine from the parser, producing the "compiled" data.
    /// </summary>
    void LoadStateMachine() {
        states = new Dictionary<string, GameState>();
        events = new Dictionary<string, GameEvent>();
        foreach (ParseSM.State s in parser.stateInfo) {
            GameState gs = new GameState();
            gs.name = s.name;
            gs.scene = s.scene;
            gs.isAdditive = s.additive;
            gs.time = s.time;
            gs.eventTransitions = new Dictionary<GameEvent, GameState>();
            gs.backTransitionEvents = new HashSet<GameEvent>();
            states.Add(s.name.ToLower(), gs);
        }

        foreach (ParseSM.TransitionEvent e in parser.eventInfo) {
            GameEvent ge = new GameEvent();
            ge.name = e.name;
            events.Add(ge.name.ToLower(), ge);
        }

        foreach (ParseSM.State s in parser.stateInfo) {
            if (s.timedTransition != null) {
                states[s.name.ToLower()].timedTransition = states[s.timedTransition.ToLower()];
            }
            if (s.eventTransitions != null) {
                foreach (string key in s.eventTransitions.Keys) {
                    states[s.name.ToLower()].eventTransitions[events[key.ToLower()]] = states[s.eventTransitions[key].ToLower()];
                }
            }
            if (s.backTransitionEvents != null) {
                foreach (string key in s.backTransitionEvents) {
                    states[s.name.ToLower()].backTransitionEvents.Add(events[key.ToLower()]);
                }
            }
            if (s.name.ToLower() == parser.startState.ToLower()) {
                currentState = states[s.name.ToLower()];
            }
            if (s.name.ToLower() == parser.errorState.ToLower()) {
                errorState = states[s.name.ToLower()];
            }
        }

        eventsPending.Clear();
        history.Clear();
        StartCoroutine("RunCurrentState");
    }

    /// <summary>
    /// Show a summary of the state machine in the Inspector
    /// </summary>
    void ShowStateMachine() {
        message = "";
        foreach (GameState gs in states.Values) {
            if (gs == currentState) {
                message += " ---> ";
            }
            if (gs == errorState) {
                message += " <Error state> ";
            }
            message += gs.name;
            if (gs.scene != null && gs.scene.Length > 0) {
                message += "[" + gs.scene + "]";
            }
            message += ":";
            if (gs.timedTransition != null) {
                message += " " + gs.timedTransition.name + "[" + gs.time + "]";
            }
            foreach (GameEvent e in gs.eventTransitions.Keys) {
                GameState s = gs.eventTransitions[e];
                message += " " + s.name + "[" + e.name + "]";
            }
            foreach (GameEvent e in gs.backTransitionEvents) {
                message += " <go back>[" + e.name + "]";
            }
            message += "\n";
        }
    }

    /// <summary>
    /// Things that must be done on entering a new state
    /// </summary>
    /// <returns></returns>
    IEnumerator RunCurrentState() {
        if ((parser.logFlags & ParseSM.LogFlags.ENTRY) != 0) {
            Debug.Log("Entering state '" + currentState.name + "'");
        }
        ClearEvents();
        foreach (GameEvent e in currentState.eventTransitions.Keys) {
            RegisterEvent(e);
        }
        foreach (GameEvent e in currentState.backTransitionEvents) {
            RegisterEvent(e);
        }

        AsyncOperation op = null;

        if (unloadInsteadOfLoadNew != null && unloadInsteadOfLoadNew.Length > 0) {
            op = SceneManager.UnloadSceneAsync(unloadInsteadOfLoadNew);
            unloadInsteadOfLoadNew = null;
        } else if (currentState.scene != null && currentState.scene.Length > 0) {
            unloadInsteadOfLoadNew = null;
            if (currentState.isAdditive) {
                op = SceneManager.LoadSceneAsync(currentState.scene, LoadSceneMode.Additive);
            } else {
                op = SceneManager.LoadSceneAsync(currentState.scene);
            }
        }
        while (op != null && !op.isDone) {
            yield return null;
        }
        if (op != null && op.isDone) {
            if ((parser.logFlags & ParseSM.LogFlags.SCENE) != 0) {
                Debug.Log("Scene " + currentState.scene + " loaded for state '" + currentState.name + "'");
            }
        }
        bool waitingForTransition = true;
        float time = Time.time;
        while (waitingForTransition) {
            if (currentState.timedTransition != null) {
                if (Time.time - time > currentState.time) {
                    if ((parser.logFlags & ParseSM.LogFlags.TIMED) != 0) {
                        Debug.Log("Timed transition event from state '" + currentState.name + "' to state '" + currentState.timedTransition.name + "'");
                    }
                    waitingForTransition = false;
                    Transition(currentState.timedTransition);
                    break;
                }
            }

            while (eventsPending.Count > 0) {
                GameEvent e = eventsPending.Dequeue();
                if (currentState.eventTransitions.ContainsKey(e)) {
                    if ((parser.logFlags & ParseSM.LogFlags.NAMED) != 0) {
                        Debug.Log("Named transition event '" + e.name + "' from state '" + currentState.name + "' to state '" + currentState.eventTransitions[e].name + "'");
                    }
                    waitingForTransition = false;
                    Transition(currentState.eventTransitions[e]);
                    break;
                } else if (currentState.backTransitionEvents.Contains(e)) {
                    if ((parser.logFlags & ParseSM.LogFlags.BACK) != 0) {
                        Debug.Log("Back transition event '" + e.name + "' out of state '" + currentState.name + "'");
                    }
                    waitingForTransition = false;
                    TransitionBack();
                    break;
                } else {
                    Debug.LogError("Dropped event: " + e.name);
                }
            }

            yield return null;
        }

    }


    /// <summary>
    /// Clear event registry; called when there is a new state
    /// </summary>
    void ClearEvents() {
        registeredEvents.Clear();
    }


    /// <summary>
    /// Add an event to the registry for the currents state; events not registered will be dropped
    /// </summary>
    /// <param name="e"></param>
    void RegisterEvent(GameEvent e) {
        registeredEvents.Add(e);
    }


    /// <summary>
    /// Transition to the specified state
    /// </summary>
    /// <param name="state"></param>
    void Transition(GameState state) {
        if (history.Count == 0 || history.Peek() != currentState) {
            history.Push(currentState);
        }
        foreach (GameEvent e in eventsPending) {
            Debug.LogWarning("Possible race condition: Transition resulted in dropped event: " + e.name);
        }
        eventsPending.Clear();

        if ((parser.logFlags & ParseSM.LogFlags.EXIT) != 0) {
            Debug.Log("Exiting state " + currentState.name);
        }

        currentState = state;
        StartCoroutine("RunCurrentState");
        ShowStateMachine();
    }


    /// <summary>
    /// Transition to the previous state in history
    /// </summary>
    void TransitionBack() {
        if (currentState.isAdditive && currentState.scene != null && currentState.scene.Length > 0) {
            unloadInsteadOfLoadNew = currentState.scene;
        }
        Transition(history.Pop());
    }

}
