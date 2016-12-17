using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Used by States for default "logging" actions
/// </summary>
public class DefaultActions : MonoBehaviour {

    StateMachine sm;
    
    public void OnEnter() {
        if (!sm) {
            sm = FindObjectOfType<StateMachine>();
        }
        Debug.Log(sm.GetActiveState().name + " Entered at " + Time.time);
    }

    public void BeforeExit() {
        if (!sm) {
            sm = FindObjectOfType<StateMachine>();
        }
        Debug.Log(sm.GetActiveState().name + " exited at " + Time.time);
    }

    public void OnSceneLoaded() {
        if (!sm) {
            sm = FindObjectOfType<StateMachine>();
        }
        Debug.Log(sm.GetActiveState().name + ": Scene " + sm.GetActiveState().loadThisScene + " loaded at " + Time.time);
    }
}
