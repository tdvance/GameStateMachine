using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Navigation : MonoBehaviour {
    StateMachine sm;

    private void Start() {
        sm = FindObjectOfType<StateMachine>();
    }

    public void Transition(int which) {
       sm.Transition(which);
    }

    public void Back() {
        sm.PreviousState();
    }
}
