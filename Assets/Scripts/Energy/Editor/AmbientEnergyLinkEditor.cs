using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Lets you shape the AmbientEnergyLink path visually instead of typing
// numbers: drag any waypoint in the Scene view, and add/remove bend points
// per segment from the Inspector. Floor waypoints show blue, LED green.
[CustomEditor(typeof(AmbientEnergyLink))]
public class AmbientEnergyLinkEditor : Editor
{
    private static readonly Color FloorColor = new Color(0.25f, 0.55f, 1f);
    private static readonly Color LedColor = new Color(0.2f, 0.9f, 0.5f);

    public override void OnInspectorGUI()
    {
        var link = (AmbientEnergyLink)target;

        EditorGUI.BeginChangeCheck();

        DrawDefaultInspector();

        serializedObject.Update();
        var floorProp = serializedObject.FindProperty("floorWaypoints");
        var ledProp = serializedObject.FindProperty("ledWaypoints");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Floor path", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add bend point")) InsertMiddle(floorProp);
            GUI.enabled = floorProp.arraySize > 2;
            if (GUILayout.Button("Remove last")) floorProp.arraySize--;
            GUI.enabled = true;
        }

        EditorGUILayout.LabelField("LED path", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add bend point")) InsertMiddle(ledProp);
            GUI.enabled = ledProp.arraySize > 2;
            if (GUILayout.Button("Remove last")) ledProp.arraySize--;
            GUI.enabled = true;
        }

        serializedObject.ApplyModifiedProperties();

        // Any inspector change (points, width, glow, color, ...) rebuilds the
        // strand immediately so you see the result without entering Play.
        if (EditorGUI.EndChangeCheck()) link.Rebuild();
    }

    // Inserts a new point halfway between the first and last existing point
    // (just before the last one), ready to drag into place.
    private static void InsertMiddle(SerializedProperty listProp)
    {
        int n = listProp.arraySize;
        Vector2 a = listProp.GetArrayElementAtIndex(0).vector2Value;
        Vector2 b = listProp.GetArrayElementAtIndex(n - 1).vector2Value;
        listProp.InsertArrayElementAtIndex(n - 1);
        listProp.GetArrayElementAtIndex(n - 1).vector2Value = (a + b) * 0.5f;
    }

    private void OnSceneGUI()
    {
        var link = (AmbientEnergyLink)target;
        if (link.FloorArea == null || link.LedCanvas == null) return;

        EditorGUI.BeginChangeCheck();
        var newFloor = DrawChain(link.FloorArea, link.FloorWaypoints, FloorColor, "F");
        var newLed = DrawChain(link.LedCanvas, link.LedWaypoints, LedColor, "L");
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(link, "Move Ambient Energy Link Point");
            link.FloorWaypoints.Clear();
            link.FloorWaypoints.AddRange(newFloor);
            link.LedWaypoints.Clear();
            link.LedWaypoints.AddRange(newLed);
            EditorUtility.SetDirty(link);
            link.Rebuild();
        }
    }

    private static List<Vector2> DrawChain(RectTransform area, List<Vector2> points, Color color, string prefix)
    {
        var result = new List<Vector2>(points.Count);
        Handles.color = color;
        Vector3? prevWorld = null;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 world = area.TransformPoint(new Vector3(points[i].x, points[i].y, 0f));
            float size = HandleUtility.GetHandleSize(world) * 0.08f;
            Handles.Label(world + Vector3.up * size * 2f, prefix + i);
            Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
            result.Add(area.InverseTransformPoint(moved));

            if (prevWorld.HasValue) Handles.DrawLine(prevWorld.Value, moved);
            prevWorld = moved;
        }

        return result;
    }
}
