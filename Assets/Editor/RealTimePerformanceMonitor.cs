using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Unity.Profiling;
using System.Linq;

public class RealTimePerformanceMonitor : EditorWindow
{
    private const int graphWidth = 300;
    private const int graphHeight = 100;
    private const int maxSamples = 300;

    private Queue<float> fpsSamples = new Queue<float>();
    private Queue<float> cpuTimeSamples = new Queue<float>();
    private Queue<float> memoryUsageSamples = new Queue<float>();

    private float updateInterval = 0.5f;
    private double lastUpdateTime;

    private ProfilerRecorder cpuTimeRecorder;

    [MenuItem("Window/Real-Time Performance Monitor")]
    public static void ShowWindow()
    {
        GetWindow<RealTimePerformanceMonitor>("Performance Monitor");
    }

    private void OnEnable()
    {
        fpsSamples.Clear();
        cpuTimeSamples.Clear();
        memoryUsageSamples.Clear();

        cpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", maxSamples);
    }

    private void OnDisable()
    {
        if (cpuTimeRecorder.Valid)
            cpuTimeRecorder.Dispose();
    }

    private void Update()
    {
        if (EditorApplication.isPlaying)
        {
            Repaint();

            if (EditorApplication.timeSinceStartup - lastUpdateTime > updateInterval)
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;

                // Collect FPS data
                float fps = 1.0f / Time.deltaTime;
                fpsSamples.Enqueue(fps);
                if (fpsSamples.Count > maxSamples)
                    fpsSamples.Dequeue();

                // Collect CPU Time data
                if (cpuTimeRecorder.Valid)
                {
                    float cpuTime = cpuTimeRecorder.LastValue * (1e-6f); // Convert from nanoseconds to milliseconds
                    cpuTimeSamples.Enqueue(cpuTime);
                }
                else
                {
                    cpuTimeSamples.Enqueue(0f);
                }
                if (cpuTimeSamples.Count > maxSamples)
                    cpuTimeSamples.Dequeue();

                // Collect Memory Usage data
                float memoryUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
                memoryUsageSamples.Enqueue(memoryUsage);
                if (memoryUsageSamples.Count > maxSamples)
                    memoryUsageSamples.Dequeue();
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Real-Time Performance Monitoring", EditorStyles.boldLabel);

        using (var scrollView = new EditorGUILayout.ScrollViewScope(Vector2.zero))
        {
            // FPS Graph
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Frame Rate (FPS): " + GetLatestSample(fpsSamples));
            DrawGraph(fpsSamples, 0, 500, Color.green);
            EditorGUILayout.EndVertical();

            // CPU Time Graph
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("CPU Time (ms): " + GetLatestSample(cpuTimeSamples));
            DrawGraph(cpuTimeSamples, 0, 50, Color.yellow);
            EditorGUILayout.EndVertical();

            // Memory Usage Graph
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Memory Usage (MB): " + GetLatestSample(memoryUsageSamples));
            DrawGraph(memoryUsageSamples, 0, 500, Color.cyan);
            EditorGUILayout.EndVertical();
        }
    }

    private string GetLatestSample(Queue<float> samples)
    {
        return samples.Count > 0 ? samples.Last().ToString("F2") : "N/A";
    }

    private void DrawGraph(Queue<float> samples, float minValue, float maxValue, Color graphColor)
    {
        Rect rect = GUILayoutUtility.GetRect(graphWidth, graphHeight);

        // Draw background
        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

        // Draw grid lines
        Handles.BeginGUI();
        Color originalColor = Handles.color;
        Handles.color = new Color(0.4f, 0.4f, 0.4f);
        for (int i = 0; i <= 10; i++)
        {
            float y = rect.y + (i / 10f) * rect.height;
            Handles.DrawLine(new Vector2(rect.x, y), new Vector2(rect.x + rect.width, y));
        }
        Handles.color = originalColor;

        // Draw graph line
        if (samples.Count > 1)
        {
            float[] samplesArray = samples.ToArray();
            int sampleCount = samplesArray.Length;

            Vector3[] points = new Vector3[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = Mathf.Clamp(samplesArray[i], minValue, maxValue);

                float x = rect.x + ((float)i / (maxSamples - 1)) * rect.width;
                float y = rect.y + rect.height - ((sample - minValue) / (maxValue - minValue)) * rect.height;
                points[i] = new Vector3(x, y, 0);
            }

            Handles.color = graphColor;
            Handles.DrawAAPolyLine(2f, points);
            Handles.color = originalColor;
        }
        Handles.EndGUI();
    }
}
