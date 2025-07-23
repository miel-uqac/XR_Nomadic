using System.Collections;
using UnityEngine;

public class UIFlowManager : MonoBehaviour
{
    [Header("Main Menus")]
    public GameObject menuUI;
    public GameObject calibrationUI1; // Standing
    public GameObject calibrationUI2; // Sitting

    [Header("Calibration Objects")]
    public GameObject cubeObject;     // Red Cube (Standing)
    public GameObject cubeObject2;    // Blue Cube (Sitting)

    [Header("Stroop Flow")]
    public GameObject preStroopUI;
    public GameObject stroopUI;

    [Header("FITT Flow")]
    public GameObject preFittUI;
    public GameObject fittUI;

    [Header("Timing")]
    public float stroopDuration = 400f; // Duration before moving to next task
    public float fittDuration = 400f;   // Duration before moving to next task

    public enum TaskOrder { StroopFirst, FittFirst }

    [Header("⚙️ Task Order")]
    public TaskOrder chosenOrder = TaskOrder.StroopFirst;

    // ▶️ Called when "Start" is clicked
    public void OnStartButtonClicked()
    {
        menuUI.SetActive(false);
        calibrationUI1.SetActive(true);
        cubeObject.SetActive(true);
    }

    public void OnStandingCalibrationComplete()
    {
        calibrationUI1.SetActive(false);
        cubeObject.SetActive(false);

        calibrationUI2.SetActive(true);
        cubeObject2.SetActive(true);
    }

    public void OnSittingCalibrationComplete()
    {
        calibrationUI2.SetActive(false);
        cubeObject2.SetActive(false);

        // Decide which test starts first
        if (chosenOrder == TaskOrder.StroopFirst)
            preStroopUI.SetActive(true);
        else
            preFittUI.SetActive(true);
    }

    // ▶️ Pre-Stroop -> Stroop
    public void OnPreStroopNext()
    {
        preStroopUI.SetActive(false);
        stroopUI.SetActive(true);

        StartCoroutine(DelayToPreFittUI());
    }

    IEnumerator DelayToPreFittUI()
    {
        yield return new WaitForSeconds(stroopDuration);

        stroopUI.SetActive(false);
        preFittUI.SetActive(true);
    }

    // ▶️ Pre-FITT -> FITT
    public void OnPreFittNext()
    {
        preFittUI.SetActive(false);
        fittUI.SetActive(true);

        // If FITT was first, show Stroop after duration
        if (chosenOrder == TaskOrder.FittFirst)
        {
            StartCoroutine(DelayToPreStroopAfterFitt());
        }
    }

    IEnumerator DelayToPreStroopAfterFitt()
    {
        yield return new WaitForSeconds(fittDuration);

        fittUI.SetActive(false);
        preStroopUI.SetActive(true);
    }
}
