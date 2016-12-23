using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;

/// <summary>
/// The parser for state machine program files
/// </summary>
public class ParseSM : MonoBehaviour {
    #region Public interface
    [Tooltip("Path to .sm file; can be relative to project directory")]
    public string path;

    [Tooltip("If true, log the parser's operation; for debugging the parser's code")]
    public bool debugParser = false;

    //display that program was successfully parsed and validated
    [ReadOnly]
    public bool programIsValid = false;

    //show the parsed program in the inspector; good for debugging the state machine
    [ReadOnly]
    [TextArea(3, 20)]
    public string program;

    //show the states
    [ReadOnly]
    [TextArea(3, 20)]
    public string stateList;

    //show the events
    [ReadOnly]
    [TextArea(3, 20)]
    public string eventList;

    [ReadOnly]
    public string startState;

    [ReadOnly]
    public string errorState;

    [EnumFlags]
    public LogFlags logFlags;


    /// <summary>
    /// Get the index-th command
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public Command this[int index] {
        get {
            return commands[index];
        }
    }

    /// <summary>
    /// Return the number of commands
    /// </summary>
    public int Length {
        get { return commands.Count; }
    }

    public List<State> stateInfo {
        get {
            return states;
        }
    }

    public List<TransitionEvent> eventInfo {
        get {
            return events;
        }
    }

    #endregion

    // Parse and validate the .sm file
    void Awake() {
        programIsValid = true;//change to false if error is found

        //parse the .sm file
        text = System.IO.File.ReadAllText(path);
        stripped = RemoveComments(text);
        tokens = Tokenize(stripped);
        commands = GetProgram(tokens);

        //display the result of parsing
        program = "";
        foreach (Command c in commands) {
            program += c.ToString() + "\n";
        }

        //index the states 
        states = new List<State>();
        foreach (Command c in commands) {
            if (IsCommand(c, "State")) {
                EnsureSignature(c, "Identifier");
                State s = new State();
                s.name = c.args[0];
                states.Add(s);
            }
        }

        //check for dupes
        HashSet<string> seen = new HashSet<string>();
        foreach (State state in states) {
            if (seen.Contains(state.name.ToLower())) {
                DuplicationError("State " + state + " defined more than once");
            } else {
                seen.Add(state.name.ToLower());
            }
        }

        //display the states
        stateList = "";
        foreach (State state in states) {
            stateList += state.name + "\n";
        }

        //index the events 
        events = new List<TransitionEvent>();
        foreach (Command c in commands) {
            if (IsCommand(c, "Event")) {
                EnsureSignature(c, "Identifier");
                TransitionEvent e = new TransitionEvent();
                e.name = c.args[0];
                events.Add(e);
            }
        }

        //check for dupes
        seen.Clear();
        foreach (TransitionEvent e in events) {
            if (seen.Contains(e.name.ToLower())) {
                DuplicationError("Event " + e + " defined more than once");
            } else {
                seen.Add(e.name.ToLower());
            }
        }

        //display the events
        eventList = "";
        foreach (TransitionEvent e in events) {
            eventList += e.name + "\n";
        }

        //find startState
        startState = "";
        foreach (Command c in commands) {
            if (IsCommand(c, "DefineStart")) {
                if (startState.Length > 0) {
                    DuplicationError("DefineStart command called more than once; last call: " + c.ToString());
                }
                EnsureSignature(c, "State");
                startState = IsState(c.args[0]);
            }
        }

        //find errorState
        errorState = "";
        foreach (Command c in commands) {
            if (IsCommand(c, "DefineError")) {
                if (errorState.Length > 0) {
                    DuplicationError("errorState command called more than once; last call: " + c.ToString());
                }
                EnsureSignature(c, "State");
                errorState = IsState(c.args[0]);
            }
        }

        //Attach Scenes
        foreach (Command c in commands) {
            if (IsCommand(c, "LoadScene") || IsCommand(c, "LoadSceneAdditive")) {
                EnsureSignature(c, "State", "Identifier");
                int i = FindState(c.args[0]);
                if (states[i].scene != null && states[i].scene.Length > 0) {
                    DuplicationError("LoadScene or LoadSceneAdditive command called more than once on the same state; last call: " + c.ToString());
                }
                State s = states[i];
                s.scene = c.args[1];
                s.additive = IsCommand(c, "LoadSceneAdditive");
                states[i] = s;
            }
        }

        //Attach Timed Transitions
        foreach (Command c in commands) {
            if (IsCommand(c, "TimedTransition")) {
                EnsureSignature(c, "State", "State", "Time");
                int i = FindState(c.args[0]);
                if (states[i].timedTransition != null && states[i].timedTransition.Length > 0) {
                    DuplicationError("TimedTransition command called more than once on the same state; last call: " + c.ToString());
                }

                State s = states[i];
                s.time = float.Parse(c.args[2]);
                s.timedTransition = c.args[1];
                states[i] = s;
            }
        }

        // Attach Named Transitions
        foreach (Command c in commands) {
            if (IsCommand(c, "NamedTransition")) {
                EnsureSignature(c, "State", "State", "Event");
                int i = FindState(c.args[0]);
                TransitionEvent e = (TransitionEvent)FindEvent(c.args[2]);
                if (states[i].eventTransitions != null && states[i].eventTransitions.ContainsKey(e.name)) {
                    DuplicationError("NamedTransition command called more than once for the same event on the same state; last call: " + c.ToString());
                }

                State s = states[i];
                if (s.eventTransitions == null) {
                    s.eventTransitions = new Dictionary<string, string>();
                }
                s.eventTransitions.Add(e.name, c.args[1]);
                states[i] = s;
            }
        }

        //attach back transitions
        foreach (Command c in commands) {
            if (IsCommand(c, "BackTransition")) {
                EnsureSignature(c, "State", "Event");
                int i = FindState(c.args[0]);
                TransitionEvent e = (TransitionEvent)FindEvent(c.args[1]);
                //TODO
                if (states[i].backTransitionEvents != null && states[i].backTransitionEvents.Contains(e.name)) {
                    DuplicationError("BackTransition command called more than once for the same event on the same state; last call: " + c.ToString());
                }

                State s = states[i];
                if (s.backTransitionEvents == null) {
                    s.backTransitionEvents = new HashSet<string>();
                }
                s.backTransitionEvents.Add(e.name);
                states[i] = s;
            }
        }

        //Set Log Flags

        logFlags = 0;

        foreach (Command c in commands) {

            if (IsCommand(c, "LogStateEntry")) {
                if ((logFlags & LogFlags.ENTRY) != 0) {
                    DuplicationError("LogStateEntry command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.ENTRY;
            }

            if (IsCommand(c, "LogStateExit")) {
                if ((logFlags & LogFlags.EXIT) != 0) {
                    DuplicationError("LogStateExit command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.EXIT;
            }

            if (IsCommand(c, "LogSceneLoaded")) {
                if ((logFlags & LogFlags.SCENE) != 0) {
                    DuplicationError("LogSceneLoaded command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.SCENE;
            }

            if (IsCommand(c, "LogTimedTransitions")) {
                if ((logFlags & LogFlags.TIMED) != 0) {
                    DuplicationError("LogTimedTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.TIMED;
            }

            if (IsCommand(c, "LogNamedTransitions")) {
                if ((logFlags & LogFlags.NAMED) != 0) {
                    DuplicationError("LogNamedTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.NAMED;
            }

            if (IsCommand(c, "LogBackTransitions")) {
                if ((logFlags & LogFlags.BACK) != 0) {
                    DuplicationError("LogBackTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.BACK;
            }

            if (IsCommand(c, "LogAllTransitions")) {
                if ((logFlags & LogFlags.TIMED) != 0) {
                    DuplicationError("LogTimedTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.TIMED;
                if ((logFlags & LogFlags.NAMED) != 0) {
                    DuplicationError("LogNamedTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.NAMED;
                if ((logFlags & LogFlags.BACK) != 0) {
                    DuplicationError("LogBackTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.BACK;
            }

            if (IsCommand(c, "LogAll")) {
                if ((logFlags & LogFlags.ENTRY) != 0) {
                    DuplicationError("LogStateEntry command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.ENTRY;
                if ((logFlags & LogFlags.EXIT) != 0) {
                    DuplicationError("LogStateExit command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.EXIT;
                if ((logFlags & LogFlags.SCENE) != 0) {
                    DuplicationError("LogSceneLoaded command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.SCENE;
                if ((logFlags & LogFlags.TIMED) != 0) {
                    DuplicationError("LogTimedTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.TIMED;
                if ((logFlags & LogFlags.NAMED) != 0) {
                    DuplicationError("LogNamedTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.NAMED;
                if ((logFlags & LogFlags.BACK) != 0) {
                    DuplicationError("LogBackTransitions command called more than once; last call: " + c.ToString());
                }
                logFlags |= LogFlags.BACK;
            }

        }

    }

    #region State class

    [Serializable]
    public struct State {
        public string name;
        public string scene;
        public bool additive;
        [Space]
        public string timedTransition;
        public float time;
        [Space]
        public Dictionary<string, string> eventTransitions;//event maps to state
        public HashSet<string> backTransitionEvents;

        public override string ToString() {
            string result = name + "[scene=" + scene + ", timedTransition=" + timedTransition + "(" + time + ")]";
            return result;
        }

    }

    #endregion


    #region TransitionEvent class

    [Serializable]
    public struct TransitionEvent {
        public string name;

    }

    #endregion


    #region Command class
    [Serializable]
    public struct Command {

        public string name;
        public List<String> args;
        public int commandNum;


        override public string ToString() {
            string result = name + "(";
            bool first = true;
            foreach (string arg in args) {
                if (first) {
                    first = false;
                } else {
                    result += ", ";
                }
                result += arg;
            }
            result += ");";
            return result;
        }
    }
    #endregion


    #region EnumFlagsAttribute definition
    public class EnumFlagsAttribute : PropertyAttribute {
        public EnumFlagsAttribute() { }
    }

    [CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
    public class EnumFlagsAttributeDrawer : PropertyDrawer {
        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            _property.intValue = EditorGUI.MaskField(_position, _label, _property.intValue, _property.enumNames);
        }
    }
    #endregion

    #region ReadOnlyAttribute definition
    public class ReadOnlyAttribute : PropertyAttribute {

    }

    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property,
                                                GUIContent label) {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position,
                                   SerializedProperty property,
                                   GUIContent label) {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
    #endregion

    #region Internal properties

    string text;
    string stripped;
    List<string> tokens;
    List<Command> commands;
    List<State> states;
    List<TransitionEvent> events;

    [Serializable]
    [Flags]
    public enum LogFlags { ENTRY = 1, EXIT = 2, SCENE = 4, TIMED = 8, NAMED = 16, BACK = 32 }


    string[] allowedCommands = {
        "DefineStart",
        "DefineError",
        "State",
        "LoadScene",
        "LoadSceneAdditive",
        "TimedTransition",
        "Event",
        "NamedTransition",
        "BackTransition",
        "LogStateEntry",
        "LogStateExit",
        "LogSceneLoaded",
        "LogTimedTransitions",
        "LogNamedTransitions",
        "LogBackTransitions",
        "LogAllTransitions",
        "LogAll",
    };


    char[] whitespace = new char[] { ' ', '\t', '\n', '\r' };
    string delimiters = "(),;";
    #endregion

    #region Internal Methods

    bool IsCommand(Command test, string expected = null) {
        return (expected == null || allowedCommands[test.commandNum].ToLower() == expected.ToLower());
    }


    int FindState(string s) {
        for (int i = 0; i < states.Count; i++) {
            if (s.ToLower() == states[i].name.ToLower()) {
                return i;
            }
        }
        return -1;
    }

    string IsState(string s) {
        foreach (State state in states) {
            if (s.ToLower() == state.name.ToLower()) {
                return state.name;
            }
        }

        return null;
    }

    TransitionEvent? FindEvent(string s) {
        foreach (TransitionEvent e in events) {
            if (s.ToLower() == e.name.ToLower()) {
                return e;
            }
        }

        return null;
    }

    string IsEvent(string s) {
        foreach (TransitionEvent e in events) {
            if (s.ToLower() == e.name.ToLower()) {
                return e.name;
            }
        }

        return null;
    }

    string RemoveComments(string text) {
        string result = "";
        bool inComment = false;
        foreach (char c in text) {
            if (c == '#') {
                inComment = true;
            } else if (c == '\n' || c == '\r') {
                inComment = false;
            }
            if (!inComment) {
                result += c;
            }
        }
        return result;
    }

    List<string> Tokenize(string program) {
        string[] t1 = program.Split(whitespace);
        List<string> result = new List<string>();

        foreach (string s in t1) {
            string t = "";
            foreach (char c in s) {
                if (delimiters.Contains(c.ToString())) {
                    if (t != "") {
                        result.Add(t);
                    }
                    result.Add(c.ToString());
                    t = "";
                } else {
                    t += c;
                }
            }
        }

        return result;
    }


    List<Command> GetProgram(List<String> tokens) {
        if (debugParser) {
            Debug.Log("Getting program");
        }
        List<Command> program = new List<Command>();
        int i = 0;
        while (i < tokens.Count) {
            int lastI = i;
            Command c = GetCommand(tokens, ref i);
            if (c.name != null) {
                c.commandNum = GetCommandNum(c.name);
                if (c.commandNum < 0) {
                    UnrecognizedCommandError(tokens, i, c.name);
                } else {
                    program.Add(c);
                }
            }
            if (GetToken(tokens, ";", ref i) == null) {
                SyntaxError(tokens, i, ";");
            }
            if (lastI == i) {
                i++;//try to keep parsing if there is a syntax error
            }
        }
        return program;
    }

    Command GetCommand(List<String> tokens, ref int index) {
        if (debugParser) {
            Debug.Log("Getting command");
        }
        Command c = new Command();
        c.name = null;
        c.args = new List<string>();

        string keyword = GetIdentifier(tokens, ref index);
        if (keyword == null) {
            SyntaxError(tokens, index, "<Identifier>");
        } else {
            c.name = keyword;
            if (debugParser) {
                Debug.Log("Got keyword " + c.name);
            }
        }
        if (GetToken(tokens, "(", ref index) == null) {
            SyntaxError(tokens, index, "(");
        }
        if (GetToken(tokens, ")", ref index) != null) {
            return c;
        }

        c.args = GetArgList(tokens, ref index);
        if (c.args.Count == 0) {
            c.name = null;
        }

        if (GetToken(tokens, ")", ref index) == null) {
            SyntaxError(tokens, index, ")");
            c.name = null;
        }
        return c;
    }

    string GetToken(List<String> tokens, string expected, ref int index) {
        if (tokens[index].ToLower() == expected.ToLower()) {
            index++;
            return tokens[index - 1];
        } else {
            return null;
        }
    }

    string GetIdentifier(List<String> tokens, ref int index) {
        if (IsIdentifier(tokens[index])) {
            index++;
            return tokens[index - 1];
        } else {
            return null;
        }
    }


    bool IsIdentifier(string s) {
        if (!char.IsLetter(s[0]) && !(s[0] == '_')) {
            return false;
        }
        foreach (char c in s) {
            if (!char.IsLetterOrDigit(s[0]) && !(s[0] == '_')) {
                return false;
            }
        }
        return true;
    }

    List<string> GetArgList(List<String> tokens, ref int index) {
        if (debugParser) {
            Debug.Log("Getting arglist");
        }
        List<string> argList = new List<string>();
        while (true) {
            int lastIndex = index;
            string arg = GetIdentifier(tokens, ref index);
            if (arg == null) {
                arg = GetFloat(tokens, ref index);
            }
            if (arg == null) {
                SyntaxError(tokens, index, "<Identifier or Float>");
            } else {
                if (debugParser) {
                    Debug.Log("Got arg " + arg);
                }
                argList.Add(arg);
            }
            if (GetToken(tokens, ",", ref index) == null) {
                if (debugParser) {
                    Debug.Log("Got arglist " + argList.ToString());
                }
                return argList;
            }
            if (index == lastIndex) {
                //error
                return new List<string>();
            }
        }
    }

    int GetCommandNum(string name) {
        for (int i = 0; i < allowedCommands.Length; i++) {
            if (name.ToLower() == allowedCommands[i].ToLower()) {
                return i;
            }
        }
        Debug.LogError("Parse error: unrecognized command '" + name + "'");
        return -1;
    }

    string GetFloat(List<String> tokens, ref int index) {
        if (IsFloat(tokens[index])) {
            index++;
            return tokens[index - 1];
        } else {
            return null;
        }
    }


    bool IsFloat(string s) {
        try {
            float.Parse(s);
            return true;
        } catch (FormatException) {
            return false;
        }
    }

    void EnsureSignature(Command c, params string[] types) {
        string message = c.name + " command takes " + types.Length + " argument(s) of type(s): ";
        bool first = true;
        foreach (string t in types) {
            if (first) {
                first = false;
            } else {
                message += ", ";
            }
            message += "<" + t + ">";
        }
        if (c.args.Count != types.Length) {
            ValidationError(c, message);
        }
        for (int i = 0; i < types.Length; i++) {
            switch (types[i].ToLower()) {
                case "identifier":
                    if (!IsIdentifier(c.args[i])) {
                        ValidationError(c, message);
                    }
                    break;
                case "state":
                    if (!IsIdentifier(c.args[i]) || IsState(c.args[i]) == null) {
                        ValidationError(c, message);
                    }
                    break;
                case "event":
                    if (!IsIdentifier(c.args[i]) || IsEvent(c.args[i]) == null) {
                        ValidationError(c, message);
                    }

                    break;
                case "time":
                    if (!IsFloat(c.args[i])) {
                        ValidationError(c, message);
                    }
                    break;
                case "float":
                    if (!IsFloat(c.args[i])) {
                        ValidationError(c, message);
                    }
                    break;
                default:
                    break;
            }
        }

    }


    #endregion

    #region Error methods
    void SyntaxError(List<string> tokens, int index, string expected) {
        programIsValid = false;
        string context = "";
        int iStart = Math.Max(index - 3, 0);
        int iEnd = Math.Min(index + 4, tokens.Count);
        for (int i = iStart; i < iEnd; i++) {
            if (i > iStart) {
                context += " ";
            }
            if (i == index) {
                context += "<color='green'>";
            }
            context += tokens[i];
            if (i == index) {
                context += "</color>";
            }
        }

        Debug.LogError("Syntax Error at: " + context + "; expected: " + expected);
    }

    void UnrecognizedCommandError(List<string> tokens, int index, string name) {
        programIsValid = false;
        string context = "";
        int iStart = Math.Max(index - 3, 0);
        int iEnd = Math.Min(index + 4, tokens.Count);
        for (int i = iStart; i < iEnd; i++) {
            if (i > iStart) {
                context += " ";
            }
            if (i == index) {
                context += "<color='green'>";
            }
            context += tokens[i];
            if (i == index) {
                context += "</color>";
            }
        }
        Debug.LogError("Unrecognized Command Error at: " + context + "; not a command: " + name);
    }

    void ValidationError(Command c, string message) {
        programIsValid = false;
        Debug.LogError("Validation error at: " + c.ToString() + "; " + message);
    }

    void DuplicationError(string message) {
        programIsValid = false;
        Debug.LogError("Duplication Error: " + message);
    }


    #endregion
}
