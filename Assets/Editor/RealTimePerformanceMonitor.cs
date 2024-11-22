using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Profiling;

public class RealTimePerformanceMonitor : EditorWindow
{
    private const int graphWidth = 300;
    private const int graphHeight = 100;
    private const int maxSamples = 300;

    private List<float> fpsSamples = new List<float>();
    private List<float> cpuTimeSamples = new List<float>();
    private List<float> gpuTimeSamples = new List<float>();
    private List<float> drawCallsSamples = new List<float>();
    private List<float> memoryUsageSamples = new List<float>();

    private float updateInterval = 0.5f;
    private double lastUpdateTime;

    private ProfilerRecorder cpuTimeRecorder;
    private ProfilerRecorder gpuTimeRecorder;
    private ProfilerRecorder drawCallsRecorder;

    private Vector2 scrollPosition = Vector2.zero;

    // Settings
    private bool showFPS = true;
    private float maxFPSValue = 500f;

    private bool showCPUTime = true;
    private float maxCPUTimeValue = 50f;

    private bool showGPUTime = true;
    private float maxGPUTimeValue = 50f;

    private bool showDrawCalls = true;
    private float maxDrawCallsValue = 1000f;

    private bool showMemoryUsage = true;
    private float maxMemoryUsageValue = 1024;

    private bool showSettings = false;

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
        drawCallsSamples.Clear();
        memoryUsageSamples.Clear();

        // Start the ProfilerRecorders
        cpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", maxSamples);
        gpuTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread", maxSamples);
        drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", maxSamples);

        // Load settings
        LoadSettings();
    }

    private void OnDisable()
    {
        // Dispose of the ProfilerRecorders if they're valid
        if (cpuTimeRecorder.Valid)
            cpuTimeRecorder.Dispose();

        if (gpuTimeRecorder.Valid)
            gpuTimeRecorder.Dispose();

        if (drawCallsRecorder.Valid)
            drawCallsRecorder.Dispose();

        // Save settings
        SaveSettings();
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
                if (showFPS)
                {
                    float fps = 1.0f / Time.deltaTime;
                    fpsSamples.Add(fps);
                    TrimSamples(fpsSamples);
                }

                // Collect CPU Time data
                if (showCPUTime)
                {
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
                }

                // Collect GPU Time data
                if (showGPUTime)
                {
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
                }

                // Collect Draw Calls data
                if (showDrawCalls)
                {
                    if (drawCallsRecorder.Valid)
                    {
                        float drawCalls = drawCallsRecorder.LastValue;
                        drawCallsSamples.Add(drawCalls);
                    }
                    else
                    {
                        drawCallsSamples.Add(0f);
                    }
                    TrimSamples(drawCallsSamples);
                }

                // Collect Memory Usage data
                if (showMemoryUsage)
                {
                    float memoryUsage = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
                    memoryUsageSamples.Add(memoryUsage);
                    TrimSamples(memoryUsageSamples);
                }
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Real-Time Performance Monitoring", EditorStyles.boldLabel);

        // Settings Foldout
        showSettings = EditorGUILayout.Foldout(showSettings, "Settings");
        if (showSettings)
        {
            EditorGUI.indentLevel++;
            updateInterval = EditorGUILayout.Slider("Update Interval (s)", updateInterval, 0.1f, 5f);

            // FPS Settings
            showFPS = EditorGUILayout.Toggle("Show FPS", showFPS);
            if (showFPS)
            {
                EditorGUI.indentLevel++;
                maxFPSValue = EditorGUILayout.FloatField("Max FPS Value", maxFPSValue);
                EditorGUI.indentLevel--;
            }

            // CPU Time Settings
            showCPUTime = EditorGUILayout.Toggle("Show CPU Time", showCPUTime);
            if (showCPUTime)
            {
                EditorGUI.indentLevel++;
                maxCPUTimeValue = EditorGUILayout.FloatField("Max CPU Time (ms)", maxCPUTimeValue);
                EditorGUI.indentLevel--;
            }

            // GPU Time Settings
            showGPUTime = EditorGUILayout.Toggle("Show GPU Time", showGPUTime);
            if (showGPUTime)
            {
                EditorGUI.indentLevel++;
                maxGPUTimeValue = EditorGUILayout.FloatField("Max GPU Time (ms)", maxGPUTimeValue);
                EditorGUI.indentLevel--;
            }

            // Draw Calls Settings
            showDrawCalls = EditorGUILayout.Toggle("Show Draw Calls", showDrawCalls);
            if (showDrawCalls)
            {
                EditorGUI.indentLevel++;
                maxDrawCallsValue = EditorGUILayout.FloatField("Max Draw Calls", maxDrawCallsValue);
                EditorGUI.indentLevel--;
            }

            // Memory Usage Settings
            showMemoryUsage = EditorGUILayout.Toggle("Show Memory Usage", showMemoryUsage);
            if (showMemoryUsage)
            {
                EditorGUI.indentLevel++;
                maxMemoryUsageValue = EditorGUILayout.FloatField("Max Memory Usage (MB)", maxMemoryUsageValue);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // Use a scroll view in case the window is too small
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        {
            if (showFPS)
            {
                // FPS Graph
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Frame Rate (FPS): " + GetLatestSample(fpsSamples));
                DrawGraph(fpsSamples, 0, maxFPSValue, Color.green);
                EditorGUILayout.EndVertical();
            }

            if (showCPUTime)
            {
                // CPU Time Graph
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("CPU Time (ms): " + GetLatestSample(cpuTimeSamples));
                DrawGraph(cpuTimeSamples, 0, maxCPUTimeValue, Color.yellow);
                EditorGUILayout.EndVertical();
            }

            if (showGPUTime)
            {
                // GPU Time Graph
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("GPU Time (ms): " + GetLatestSample(gpuTimeSamples));
                DrawGraph(gpuTimeSamples, 0, maxGPUTimeValue, Color.magenta);
                EditorGUILayout.EndVertical();
            }

            if (showDrawCalls)
            {
                // Draw Calls Graph
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Draw Calls: " + GetLatestSample(drawCallsSamples));
                DrawGraph(drawCallsSamples, 0, maxDrawCallsValue, Color.blue);
                EditorGUILayout.EndVertical();
            }

            if (showMemoryUsage)
            {
                // Memory Usage Graph
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("Memory Usage (MB): " + GetLatestSample(memoryUsageSamples));
                DrawGraph(memoryUsageSamples, 0, maxMemoryUsageValue, Color.cyan);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
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

    private void SaveSettings()
    {
        EditorPrefs.SetBool("RTPM_ShowFPS", showFPS);
        EditorPrefs.SetFloat("RTPM_MaxFPSValue", maxFPSValue);

        EditorPrefs.SetBool("RTPM_ShowCPUTime", showCPUTime);
        EditorPrefs.SetFloat("RTPM_MaxCPUTimeValue", maxCPUTimeValue);

        EditorPrefs.SetBool("RTPM_ShowGPUTime", showGPUTime);
        EditorPrefs.SetFloat("RTPM_MaxGPUTimeValue", maxGPUTimeValue);

        EditorPrefs.SetBool("RTPM_ShowDrawCalls", showDrawCalls);
        EditorPrefs.SetFloat("RTPM_MaxDrawCallsValue", maxDrawCallsValue);

        EditorPrefs.SetBool("RTPM_ShowMemoryUsage", showMemoryUsage);
        EditorPrefs.SetFloat("RTPM_MaxMemoryUsageValue", maxMemoryUsageValue);

        EditorPrefs.SetFloat("RTPM_UpdateInterval", updateInterval);
    }

    private void LoadSettings()
    {
        showFPS = EditorPrefs.GetBool("RTPM_ShowFPS", true);
        maxFPSValue = EditorPrefs.GetFloat("RTPM_MaxFPSValue", 500f);

        showCPUTime = EditorPrefs.GetBool("RTPM_ShowCPUTime", true);
        maxCPUTimeValue = EditorPrefs.GetFloat("RTPM_MaxCPUTimeValue", 50f);

        showGPUTime = EditorPrefs.GetBool("RTPM_ShowGPUTime", true);
        maxGPUTimeValue = EditorPrefs.GetFloat("RTPM_MaxGPUTimeValue", 50f);

        showDrawCalls = EditorPrefs.GetBool("RTPM_ShowDrawCalls", true);
        maxDrawCallsValue = EditorPrefs.GetFloat("RTPM_MaxDrawCallsValue", 1000f);

        showMemoryUsage = EditorPrefs.GetBool("RTPM_ShowMemoryUsage", true);
        maxMemoryUsageValue = EditorPrefs.GetFloat("RTPM_MaxMemoryUsageValue", 500f);

        updateInterval = EditorPrefs.GetFloat("RTPM_UpdateInterval", 0.5f);
    }
}
