using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;

public class FittsTestUI : MonoBehaviour
{
    [System.Serializable]
    public class TestLogEntry
    {
        public string testName;
        public float reactionTime;
        public bool success;
        public int targetSize;
        public float distance;
        public float indexOfDifficulty;
        public float throughput;
        public string timestamp;
        public float screenWidth;
        public float screenHeight;
    }
    public string fittsFilename = "fitts_classic_log.csv";
    private string fittsHeader = "Timestamp;ReactionTime;Success;TargetSize;Distance;ID;Throughput;ScreenWidth;ScreenHeight";

    private List<TestLogEntry> logEntries = new List<TestLogEntry>();

    [Header("UI References")]
    public RectTransform spawnAreaRect;
    public RectTransform targetButton;
    public TextMeshProUGUI scoreText; // Assign in inspector

    [Header("Target Settings")]
    public float minTargetSize = 50f;
    public float maxTargetSize = 150f;

    private Vector2 lastTargetPos;
    private float lastTargetTime;
    private bool firstClick = true;

    void Start()
    {
        Logger.LogLine(fittsFilename, fittsHeader, "", false);  // Ensures file + header

        targetButton.SetParent(spawnAreaRect);
        targetButton.GetComponent<Button>().onClick.AddListener(OnTargetClicked);

        lastTargetPos = targetButton.anchoredPosition;
        SpawnNewTarget();
    }

    void SpawnNewTarget()
    {
        // Save current position BEFORE moving the button
        if (!firstClick)
        {
            lastTargetPos = targetButton.anchoredPosition;
        }

        float targetSize = Random.Range(minTargetSize, maxTargetSize);
        targetButton.sizeDelta = new Vector2(targetSize, targetSize);

        float width = spawnAreaRect.rect.width;
        float height = spawnAreaRect.rect.height;
        float padding = 20f;

        float minX = -width / 2 + targetSize / 2 + padding;
        float maxX = width / 2 - targetSize / 2 - padding;
        float minY = -height / 2 + targetSize / 2 + padding;
        float maxY = height / 2 - targetSize / 2 - padding;

        Vector2 newPos = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
        targetButton.anchoredPosition = newPos;

        lastTargetTime = Time.time;
    }

    void OnTargetClicked()
    {
        float reactionTime = Time.time - lastTargetTime;
        float targetSize = targetButton.sizeDelta.x;
        float distance = Vector2.Distance(lastTargetPos, targetButton.anchoredPosition);

        // Avoid invalid values on the first click
        if (firstClick)
        {
            firstClick = false;
            lastTargetPos = targetButton.anchoredPosition;
            SpawnNewTarget();
            return;
        }

        float ID = distance > 0f ? Mathf.Log((distance / targetSize) + 1, 2) : 0f;
        float throughput = reactionTime > 0f ? ID / reactionTime : 0f;

        TestLogEntry entry = new TestLogEntry
        {
            timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff"),
            reactionTime = reactionTime,
            success = true,
            targetSize = Mathf.RoundToInt(targetSize),
            distance = distance,
            indexOfDifficulty = ID,
            throughput = throughput,
            screenWidth = spawnAreaRect.rect.width,
            screenHeight = spawnAreaRect.rect.height
        };

        logEntries.Add(entry);

        Logger.LogBlock($"Timestamp: {entry.timestamp}",
        $"RT: {reactionTime:F3}s\n" +
        $"Target Size: {targetSize}px\n" +
        $"Distance: {distance:F2}px\n" +
        $"ID: {ID:F2}\n" +
        $"TP: {throughput:F2}bps\n" 
        );

        string logLine = $"{entry.timestamp};{entry.reactionTime};{entry.success};{entry.targetSize};{entry.distance};{entry.indexOfDifficulty};{entry.throughput};{entry.screenWidth};{entry.screenHeight}";
        Logger.LogLine(fittsFilename, fittsHeader, logLine, true);
        // ✅ Set the visible text
        if (scoreText != null)
        {
            scoreText.text = $"TP: {throughput:F2} bps";
        }

        SpawnNewTarget();
    }
}
