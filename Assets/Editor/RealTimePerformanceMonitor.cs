using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Profiling;
using System.IO;
using System;
using System.Linq;

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

    private List<PerformanceSample> performanceSamples = new List<PerformanceSample>();

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

    // Threshold settings
    private bool enableThresholds = false;
    private float fpsThreshold = 30f;
    private float cpuTimeThreshold = 16.67f; // Approx. 60 FPS
    private float gpuTimeThreshold = 16.67f; // Approx. 60 FPS
    private float drawCallsThreshold = 1000f;
    private float memoryUsageThreshold = 1024;

    // Logging settings
    private bool enableLogging = false;
    private bool logToConsole = true;
    private bool logToFile = false;
    private string logFilePath = "Logs/PerformanceLog.txt";

    // Reporting settings
    private bool enableReportGeneration = false;
    private float reportInterval = 60f; // Generate report every 60 seconds
    private double lastReportTime = 0;
    private string reportFilePath = "Reports/PerformanceReport.csv";

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
        LoadDebugSettings();
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
        SaveDebugSettings();
    }

    private void Update()
    {
        if (EditorApplication.isPlaying)
        {
            Repaint();

            if (EditorApplication.timeSinceStartup - lastUpdateTime > updateInterval)
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;

                var sample = new PerformanceSample
                {
                    Timestamp = DateTime.Now,
                    FPS = showFPS ? (1.0f / Time.deltaTime) : 0f,
                    CPUTime = showCPUTime && cpuTimeRecorder.Valid ? cpuTimeRecorder.LastValue * 1e-6f : 0f,
                    GPUTime = showGPUTime && gpuTimeRecorder.Valid ? gpuTimeRecorder.LastValue * 1e-5f : 0f,
                    DrawCalls = showDrawCalls && drawCallsRecorder.Valid ? drawCallsRecorder.LastValue : 0f,
                    MemoryUsage = showMemoryUsage ? UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f) : 0f
                };

                performanceSamples.Add(sample);

                // Trim samples if necessary to limit memory usage
                if (performanceSamples.Count > maxSamples)
                {
                    performanceSamples.RemoveAt(0);
                }

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

                // Check thresholds and log if necessary
                CheckThresholdsAndLog();
            }
        }

        if (enableReportGeneration && EditorApplication.timeSinceStartup - lastReportTime > reportInterval)
        {
            lastReportTime = EditorApplication.timeSinceStartup;
            GeneratePerformanceReport();
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

            // Threshold Settings
            enableThresholds = EditorGUILayout.Toggle("Enable Threshold Alerts", enableThresholds);
            if (enableThresholds)
            {
                EditorGUI.indentLevel++;
                if (showFPS)
                    fpsThreshold = EditorGUILayout.FloatField("FPS Threshold", fpsThreshold);
                if (showCPUTime)
                    cpuTimeThreshold = EditorGUILayout.FloatField("CPU Time Threshold (ms)", cpuTimeThreshold);
                if (showGPUTime)
                    gpuTimeThreshold = EditorGUILayout.FloatField("GPU Time Threshold (ms)", gpuTimeThreshold);
                if (showDrawCalls)
                    drawCallsThreshold = EditorGUILayout.FloatField("Draw Calls Threshold", drawCallsThreshold);
                if (showMemoryUsage)
                    memoryUsageThreshold = EditorGUILayout.FloatField("Memory Usage Threshold (MB)", memoryUsageThreshold);
                EditorGUI.indentLevel--;
            }

            // Logging Settings
            enableLogging = EditorGUILayout.Toggle("Enable Logging", enableLogging);
            if (enableLogging)
            {
                EditorGUI.indentLevel++;
                logToConsole = EditorGUILayout.Toggle("Log to Console", logToConsole);
                logToFile = EditorGUILayout.Toggle("Log to File", logToFile);
                if (logToFile)
                {
                    logFilePath = EditorGUILayout.TextField("Log File Path", logFilePath);
                }
                EditorGUI.indentLevel--;
            }

            // Reporting Settings
            enableReportGeneration = EditorGUILayout.Toggle("Enable Report Generation", enableReportGeneration);
            if (enableReportGeneration)
            {
                EditorGUI.indentLevel++;
                reportInterval = EditorGUILayout.FloatField("Report Interval (s)", reportInterval);
                reportFilePath = EditorGUILayout.TextField("Report File Path", reportFilePath);
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Generate Performance Report Now"))
            {
                GeneratePerformanceReport();
            }

            // Profiler Integration
            if (GUILayout.Button("Open Unity Profiler"))
            {
                EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler");
            }
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

    private void CheckThresholdsAndLog()
    {
        if (!enableThresholds && !enableLogging)
            return;

        List<string> alerts = new List<string>();

        if (enableThresholds)
        {
            // Check FPS Threshold
            if (showFPS && fpsSamples.Count > 0 && fpsSamples[fpsSamples.Count - 1] < fpsThreshold)
            {
                alerts.Add($"FPS dropped below threshold: {fpsSamples[fpsSamples.Count - 1]:F2} FPS");
            }

            // Check CPU Time Threshold
            if (showCPUTime && cpuTimeSamples.Count > 0 && cpuTimeSamples[cpuTimeSamples.Count - 1] > cpuTimeThreshold)
            {
                alerts.Add($"CPU Time exceeded threshold: {cpuTimeSamples[cpuTimeSamples.Count - 1]:F2} ms");
            }

            // Check GPU Time Threshold
            if (showGPUTime && gpuTimeSamples.Count > 0 && gpuTimeSamples[gpuTimeSamples.Count - 1] > gpuTimeThreshold)
            {
                alerts.Add($"GPU Time exceeded threshold: {gpuTimeSamples[gpuTimeSamples.Count - 1]:F2} ms");
            }

            // Check Draw Calls Threshold
            if (showDrawCalls && drawCallsSamples.Count > 0 && drawCallsSamples[drawCallsSamples.Count - 1] > drawCallsThreshold)
            {
                alerts.Add($"Draw Calls exceeded threshold: {drawCallsSamples[drawCallsSamples.Count - 1]:F0}");
            }

            // Check Memory Usage Threshold
            if (showMemoryUsage && memoryUsageSamples.Count > 0 && memoryUsageSamples[memoryUsageSamples.Count - 1] > memoryUsageThreshold)
            {
                alerts.Add($"Memory Usage exceeded threshold: {memoryUsageSamples[memoryUsageSamples.Count - 1]:F2} MB");
            }
        }

        if (alerts.Count > 0)
        {
            string message = string.Join("\n", alerts);

            // Log to Console
            if (enableLogging && logToConsole)
            {
                Debug.LogWarning("[Performance Monitor]\n" + message);
            }

            // Log to File
            if (enableLogging && logToFile)
            {
                LogToFile(message);
            }
        }
    }

    private void LogToFile(string message)
    {
        string fullPath = Path.Combine(Application.dataPath, logFilePath);
        try
        {
            File.AppendAllText(fullPath, $"[{System.DateTime.Now}] {message}\n");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to write to log file: " + e.Message);
        }
    }

    private void SaveDebugSettings()
    {
        EditorPrefs.SetBool("RTPM_EnableThresholds", enableThresholds);
        EditorPrefs.SetFloat("RTPM_FPSThreshold", fpsThreshold);
        EditorPrefs.SetFloat("RTPM_CPUTimeThreshold", cpuTimeThreshold);
        EditorPrefs.SetFloat("RTPM_GPUTimeThreshold", gpuTimeThreshold);
        EditorPrefs.SetFloat("RTPM_DrawCallsThreshold", drawCallsThreshold);
        EditorPrefs.SetFloat("RTPM_MemoryUsageThreshold", memoryUsageThreshold);

        EditorPrefs.SetBool("RTPM_EnableLogging", enableLogging);
        EditorPrefs.SetBool("RTPM_LogToConsole", logToConsole);
        EditorPrefs.SetBool("RTPM_LogToFile", logToFile);
        EditorPrefs.SetString("RTPM_LogFilePath", logFilePath);

        EditorPrefs.SetBool("RTPM_EnableReportGeneration", enableReportGeneration);
        EditorPrefs.SetFloat("RTPM_ReportInterval", reportInterval);
        EditorPrefs.SetString("RTPM_ReportFilePath", reportFilePath);
    }

    private void LoadDebugSettings()
    {
        enableThresholds = EditorPrefs.GetBool("RTPM_EnableThresholds", false);
        fpsThreshold = EditorPrefs.GetFloat("RTPM_FPSThreshold", 30f);
        cpuTimeThreshold = EditorPrefs.GetFloat("RTPM_CPUTimeThreshold", 16.67f);
        gpuTimeThreshold = EditorPrefs.GetFloat("RTPM_GPUTimeThreshold", 16.67f);
        drawCallsThreshold = EditorPrefs.GetFloat("RTPM_DrawCallsThreshold", 1000f);
        memoryUsageThreshold = EditorPrefs.GetFloat("RTPM_MemoryUsageThreshold", 1024f);

        enableLogging = EditorPrefs.GetBool("RTPM_EnableLogging", false);
        logToConsole = EditorPrefs.GetBool("RTPM_LogToConsole", true);
        logToFile = EditorPrefs.GetBool("RTPM_LogToFile", false);
        logFilePath = EditorPrefs.GetString("RTPM_LogFilePath", "Logs/PerformanceLog.txt");

        enableReportGeneration = EditorPrefs.GetBool("RTPM_EnableReportGeneration", false);
        reportInterval = EditorPrefs.GetFloat("RTPM_ReportInterval", 60f);
        reportFilePath = EditorPrefs.GetString("RTPM_ReportFilePath", "Reports/PerformanceReport.csv");
    }

    private void GeneratePerformanceReport()
    {
        if (performanceSamples.Count == 0)
        {
            Debug.Log("No performance data to report.");
            return;
        }

        string fullPath = Path.Combine(Application.dataPath, reportFilePath);
        try
        {
            using (StreamWriter writer = new StreamWriter(fullPath, true))
            {
                // Write headers if file is new
                if (new FileInfo(fullPath).Length == 0)
                {
                    writer.WriteLine("Timestamp,FPS,CPU Time (ms),GPU Time (ms),Draw Calls,Memory Usage (MB)");
                }

                // Write raw data
                foreach (var sample in performanceSamples)
                {
                    string line = $"{sample.Timestamp},{sample.FPS:F2},{sample.CPUTime:F2},{sample.GPUTime:F2},{sample.DrawCalls:F0},{sample.MemoryUsage:F2}";
                    writer.WriteLine(line);
                }

                // Calculate summaries
                float avgFPS = performanceSamples.Average(s => s.FPS);
                float minFPS = performanceSamples.Min(s => s.FPS);
                float maxFPS = performanceSamples.Max(s => s.FPS);

                float avgCPUTime = performanceSamples.Average(s => s.CPUTime);
                float minCPUTime = performanceSamples.Min(s => s.CPUTime);
                float maxCPUTime = performanceSamples.Max(s => s.CPUTime);

                float avgGPUTime = performanceSamples.Average(s => s.GPUTime);
                float minGPUTime = performanceSamples.Min(s => s.GPUTime);
                float maxGPUTime = performanceSamples.Max(s => s.GPUTime);

                float avgDrawCalls = performanceSamples.Average(s => s.DrawCalls);
                float minDrawCalls = performanceSamples.Min(s => s.DrawCalls);
                float maxDrawCalls = performanceSamples.Max(s => s.DrawCalls);

                float avgMemoryUsage = performanceSamples.Average(s => s.MemoryUsage);
                float minMemoryUsage = performanceSamples.Min(s => s.MemoryUsage);
                float maxMemoryUsage = performanceSamples.Max(s => s.MemoryUsage);

                // Write summary
                writer.WriteLine(); // Empty line before summary
                writer.WriteLine("Summary:");

                writer.WriteLine($"Average FPS:,{avgFPS:F2}");
                writer.WriteLine($"Minimum FPS:,{minFPS:F2}");
                writer.WriteLine($"Maximum FPS:,{maxFPS:F2}");

                writer.WriteLine($"Average CPU Time (ms):,{avgCPUTime:F2}");
                writer.WriteLine($"Minimum CPU Time (ms):,{minCPUTime:F2}");
                writer.WriteLine($"Maximum CPU Time (ms):,{maxCPUTime:F2}");

                writer.WriteLine($"Average GPU Time (ms):,{avgGPUTime:F2}");
                writer.WriteLine($"Minimum GPU Time (ms):,{minGPUTime:F2}");
                writer.WriteLine($"Maximum GPU Time (ms):,{maxGPUTime:F2}");

                writer.WriteLine($"Average Draw Calls:,{avgDrawCalls:F0}");
                writer.WriteLine($"Minimum Draw Calls:,{minDrawCalls:F0}");
                writer.WriteLine($"Maximum Draw Calls:,{maxDrawCalls:F0}");

                writer.WriteLine($"Average Memory Usage (MB):,{avgMemoryUsage:F2}");
                writer.WriteLine($"Minimum Memory Usage (MB):,{minMemoryUsage:F2}");
                writer.WriteLine($"Maximum Memory Usage (MB):,{maxMemoryUsage:F2}");

                writer.Flush();
            }

            performanceSamples.Clear();

            Debug.Log($"Performance report generated at: {fullPath}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to write performance report: " + e.Message);
        }
    }

    private class PerformanceSample
    {
        public DateTime Timestamp;
        public float FPS;
        public float CPUTime;
        public float GPUTime;
        public float DrawCalls;
        public float MemoryUsage;
    }
}
