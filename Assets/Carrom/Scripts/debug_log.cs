using UnityEngine;
using System.Collections;

public class debug_log : MonoBehaviour
{
    uint qsize = 15;  // number of messages to keep
    Queue myLogQueue = new Queue();

    void Start() {
        Debug.Log("Started up logging.");
    }

    void OnEnable() {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable() {
        Application.logMessageReceived -= HandleLog;
    }

void HandleLog(string logString, string stackTrace, LogType type) {
    if (type == LogType.Log) {
        myLogQueue.Enqueue("[" + type + "] : " + logString);
        while (myLogQueue.Count > qsize)
            myLogQueue.Dequeue();
    }
}

    private GUIStyle scaledStyle;

void OnGUI() {
    if (scaledStyle == null) {
        scaledStyle = new GUIStyle(GUI.skin.label);
        scaledStyle.fontSize = 10;
    }
    
    GUILayout.BeginArea(new Rect(20, -20, Screen.width, Screen.height));
    GUILayout.Label("\n" + string.Join("\n", myLogQueue.ToArray()), scaledStyle);
    GUILayout.EndArea();
}
}