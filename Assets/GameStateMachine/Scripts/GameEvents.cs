using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A convenience class that can be wired to buttons;
/// it finds the game state machine singleton and delegates to its SendEvent method
/// </summary>
public class GameEvents : MonoBehaviour {
    GameStateMachine gsm;

    // Use this for initialization
    void Start() {
        gsm = FindObjectOfType<GameStateMachine>();
    }

    public void SendEvent(string eventName) {
        gsm.SendEvent(eventName);
    }

}
