using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Unity.Profiling;

public class RealTimePerformanceMonitor : EditorWindow
{
    private const int graphWidth = 300;
    private const int graphHeight = 100;
    private const int maxSamples = 300;

    private List<float> fpsSamples = new List<float>();
    private List<float> cpuTimeSamples = new List<float>();
    private List<float> gpuTimeSamples = new List<float>();
    private List<float> memoryUsageSamples = new List<float>();

    private float updateInterval = 0.5f;
    private double lastUpdateTime;

    private ProfilerRecorder cpuTimeRecorder;
    private ProfilerRecorder gpuTimeRecorder;

    [MenuItem("Window/Real-Time Performance Monitor")]
    public static void ShowWindow()
    {
        GetWindow<RealTimePerformanceMonitor>("Performance Monitor");
    }

    private void OnEnable()
    {
        // Clear the sample lists
        fpsSamples.Clear();
        cpuTimeSamples.Clear();
        gpuTimeSamples.Clear();
        memoryUsageSamples.Clear();

        // Start the ProfilerRecorders
        cpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", maxSamples);
        gpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", maxSamples);
    }

    private void OnDisable()
    {
        // Dispose of the ProfilerRecorders
        if (cpuTimeRecorder.Valid)
            cpuTimeRecorder.Dispose();

        if (gpuTimeRecorder.Valid)
            gpuTimeRecorder.Dispose();
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
                fpsSamples.Add(fps);
                TrimSamples(fpsSamples);

                // Collect CPU Time data
                if (cpuTimeRecorder.Valid)
                {
                    float cpuTime = cpuTimeRecorder.LastValue * (1e-6f); // Convert from nanoseconds to milliseconds
                    cpuTimeSamples.Add(cpuTime);
                }
                else
                {
                    cpuTimeSamples.Add(0f);
                }
                TrimSamples(cpuTimeSamples);

                // Collect GPU Time data
                if (gpuTimeRecorder.Valid)
                {
                    float gpuTime = gpuTimeRecorder.LastValue * (1e-5f);
                    gpuTimeSamples.Add(gpuTime);
                }
                else
                {
                    gpuTimeSamples.Add(0f);
                }
                TrimSamples(gpuTimeSamples);

                // Collect Memory Usage data
                float memoryUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
                memoryUsageSamples.Add(memoryUsage);
                TrimSamples(memoryUsageSamples);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Real-Time Performance Monitoring", EditorStyles.boldLabel);

        // Use a scroll view in case the window is too small
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

            // GPU Time Graph
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("GPU Time (ms): " + GetLatestSample(gpuTimeSamples));
            DrawGraph(gpuTimeSamples, 0, 50, Color.magenta);
            EditorGUILayout.EndVertical();

            // Memory Usage Graph
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Memory Usage (MB): " + GetLatestSample(memoryUsageSamples));
            DrawGraph(memoryUsageSamples, 0, 1024, Color.cyan);
            EditorGUILayout.EndVertical();
        }
    }

    private string GetLatestSample(List<float> samples)
    {
        return samples.Count > 0 ? samples[samples.Count - 1].ToString("F2") : "N/A";
    }

    private void DrawGraph(List<float> samples, float minValue, float maxValue, Color graphColor)
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
            float y = rect.y + (i / 10.0f) * rect.height;
            Handles.DrawLine(new Vector2(rect.x, y), new Vector2(rect.x + rect.width, y));
        }
        Handles.color = originalColor;

        // Draw graph line
        if (samples.Count > 1)
        {
            Vector3[] points = new Vector3[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                // Clamp the sample value
                float sample = Mathf.Clamp(samples[i], minValue, maxValue);

                float x = rect.x + (i / (float)(maxSamples - 1)) * rect.width;
                float y = rect.y + rect.height - ((sample - minValue) / (maxValue - minValue)) * rect.height;
                points[i] = new Vector3(x, y, 0);
            }

            Handles.color = graphColor;
            Handles.DrawAAPolyLine(2f, points);
            Handles.color = originalColor;
        }
        Handles.EndGUI();
    }

    private void TrimSamples(List<float> samples)
    {
        while (samples.Count > maxSamples)
        {
            samples.RemoveAt(0);
        }
    }
}
