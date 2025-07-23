using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;

public class StroopTestManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI colorText;       // The word in the center
    public TextMeshProUGUI timerText;       // Timer below the word
    public TextMeshProUGUI scoreText;       // Top-right score display
    private float roundStartTime;
    private int roundNumber = 0;

    public Button redButton;
    public Button greenButton;
    public Button yellowButton;
    public Button blueButton;

    [Header("Game Settings")]
    public float roundTime = 10f;

    private string[] colorNames = { "ROUGE", "VERT", "JAUNE", "BLEU" };
    private Color[] unityColors = { Color.red, Color.green, Color.yellow, Color.blue };
    public string stroopFilename = "stroop_test_log.csv";
    private string stroopHeader = "Timestamp;Round;Word;InkColor;SelectedColor;TrialType;Correct;ReactionTime";
    private int currentColorIndex;   // Correct answer index (based on text color)
    private int score = 0;
    private float timer;
    private bool canClick = true;
    private bool isCongruent;
    public int maxRounds = 60;
    private string logFilePath;
    void Start()
    {
        AssignButtonListeners();
        Logger.LogLine(stroopFilename, stroopHeader, "", false); // Ensures file + header exists
        StartCoroutine(NewRound());
    }


    void Update()
    {
        if (canClick)
        {
            timer -= Time.deltaTime;
            timerText.text = timer.ToString("0.0");

            if (timer <= 0)
            {
                canClick = false;

                string trialType = isCongruent ? "Congruent" : "Incongruent";
                string logLine = $"{roundNumber},{colorText.text},{colorNames[currentColorIndex]},NO,{trialType},false,NO,{System.DateTime.Now:HH:mm:ss.fff}";

                Logger.LogLine(stroopFilename, stroopHeader, logLine, true);
                StartCoroutine(NewRound());
            }

        }
    }

    void AssignButtonListeners()
    {
        redButton.onClick.AddListener(() => OnColorSelected(0));
        greenButton.onClick.AddListener(() => OnColorSelected(1));
        yellowButton.onClick.AddListener(() => OnColorSelected(2));
        blueButton.onClick.AddListener(() => OnColorSelected(3));
    }

    void OnColorSelected(int selectedColorIndex)
    {
        if (!canClick) return;

        float reactionTime = Time.time - roundStartTime;
        bool isCorrect = selectedColorIndex == currentColorIndex;
        if (isCorrect) score++;

        scoreText.text = score.ToString();
        canClick = false;

        string trialType = isCongruent ? "Congruent" : "Incongruent";
        Logger.LogBlock($"- Timestamp: {System.DateTime.Now:HH:mm:ss.fff}",
            $"- Round: {roundNumber}\n" +
            $"- Word: {colorText.text}\n" +
            $"- Ink Color: {colorNames[currentColorIndex]}\n" +
            $"- Selected Color: {colorNames[selectedColorIndex]}\n" +
            $"- Trial Type: {trialType}\n" +
            $"- Correct: {isCorrect}\n" +
            $"- Reaction Time: {reactionTime:F3} seconds\n" +
            $"- Score: {score}\n" );

        string logLine = $"{System.DateTime.Now:HH:mm:ss.fff};{roundNumber};{colorText.text};{colorNames[currentColorIndex]};{colorNames[selectedColorIndex]};{trialType};{isCorrect};{reactionTime:F3}";
        Logger.LogLine(stroopFilename, stroopHeader, logLine, true);


        StartCoroutine(NewRound());
    }



    IEnumerator NewRound()
    {
        yield return new WaitForSeconds(0.5f);

        timer = roundTime;
        canClick = true;

        int randomWordIndex = Random.Range(0, colorNames.Length);
        currentColorIndex = Random.Range(0, unityColors.Length);

        colorText.text = colorNames[randomWordIndex];
        colorText.color = unityColors[currentColorIndex];

        isCongruent = (randomWordIndex == currentColorIndex); // ✅ NEW LINE

        roundStartTime = Time.time;
        roundNumber++;

        if (roundNumber >= maxRounds)
        {
            Debug.Log("Stroop Test complete.");
            canClick = false;
            yield break;
        }
    }

}
