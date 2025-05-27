using UnityEngine;

public class SphereWithSubliminalMotion : MonoBehaviour
{
    [Header("References")]
    public Transform headTransform;
    public Transform[] screens;

    [Header("Initial Setup")]
    public float radius = 1.5f;
    public float horizontalAngleRange = 60f;
    public Vector3 initialOffset = new Vector3(0, -0.2f, 0.8f); // POSITIVE Z = in front

    [Header("Gaze Detection")]
    public float gazeThresholdDegrees = 15f;

    [Header("Standing Detection")]
    public float standingHeightThreshold = 1.4f;

    [Header("Movement Settings")]
    public float subliminalUpwardSpeed = 0.003f; // 3mm/s
    public float extraLiftOnGazeAway = 0.006f;   // additional 6mm/s
    public float transitionSpeed = 3f;

    [Header("Standing Position (Stable Workstation)")]
    public Vector3 standingOffset = new Vector3(0, 0f, 0.6f); // Set Y=0 for exact head height
    public float keyboardAngle = 25f; // Tilt angle like a keyboard/workstation
    public float stabilityRadius = 0.5f; // Deadzone radius - screen won't move unless you go outside this

    private Vector3[] initialPositions;
    private float[] verticalLiftProgress;
    private bool wasStanding = false;
    private Vector3 stableHeadPosition; // The "center" position when standing
    private Vector3 lockedScreenPosition; // Fixed screen position
    private Quaternion lockedScreenRotation; // Fixed screen rotation
    private bool isInStandingMode = false;

    void Start()
    {
        InitializeScreens();
    }

    void InitializeScreens()
    {
        int count = screens.Length;
        initialPositions = new Vector3[count];
        verticalLiftProgress = new float[count];

        Vector3 headPos = headTransform.position;
        Vector3 headForward = headTransform.forward;
        Vector3 headRight = headTransform.right;
        Vector3 headUp = headTransform.up;

        for (int i = 0; i < count; i++)
        {
            // SIMPLE: Just put screen directly in front of user
            Vector3 screenPos = headPos + headForward * 1.2f + headUp * initialOffset.y;

            // For multiple screens, spread horizontally
            if (count > 1)
            {
                float normalizedIndex = (float)i / (count - 1);
                float horizontalOffset = Mathf.Lerp(-0.5f, 0.5f, normalizedIndex);
                screenPos += headRight * horizontalOffset;
            }

            initialPositions[i] = screenPos;
            verticalLiftProgress[i] = 0f;

            // Set initial transform
            screens[i].position = screenPos;

            // Make screen face user - screen looks AT user position horizontally only
            Vector3 lookDirection = headPos - screenPos;
            lookDirection.y = 0; // prevent pitch/roll rotation
            if (lookDirection.sqrMagnitude < 0.001f)
                lookDirection = screens[i].forward; // fallback

            screens[i].rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        Debug.Log($"Screens initialized in front of user at positions: {string.Join(", ", initialPositions)}");
    }

    void Update()
    {
        Vector3 headPos = headTransform.position;
        Vector3 headForward = headTransform.forward;
        Vector3 headUp = headTransform.up;

        bool isStanding = headPos.y >= standingHeightThreshold;

        // Handle standing transition
        if (isStanding && !wasStanding)
        {
            EnterStandingMode(headPos, headForward, headUp);
        }
        else if (!isStanding && wasStanding)
        {
            ExitStandingMode();
        }

        wasStanding = isStanding;

        // Update screen behavior
        if (isInStandingMode)
        {
            UpdateStandingMode(headPos);
        }
        else
        {
            UpdateSittingMode(headPos, headForward, headUp);
        }
    }

    void UpdateSittingMode(Vector3 headPos, Vector3 headForward, Vector3 headUp)
    {
        for (int i = 0; i < screens.Length; i++)
        {
            Vector3 dirToScreen = (screens[i].position - headPos).normalized;
            float angle = Vector3.Angle(headForward, dirToScreen);
            bool isLooking = angle < gazeThresholdDegrees;

            float liftSpeed = subliminalUpwardSpeed;
            if (!isLooking)
            {
                liftSpeed += extraLiftOnGazeAway;
            }

            verticalLiftProgress[i] += liftSpeed * Time.deltaTime;
            Vector3 targetPos = initialPositions[i] + Vector3.up * verticalLiftProgress[i];

            screens[i].position = Vector3.Lerp(screens[i].position, targetPos, Time.deltaTime * transitionSpeed);

            // Rotate horizontally only to face user
            Vector3 lookDirection = headPos - screens[i].position;
            lookDirection.y = 0;
            if (lookDirection.sqrMagnitude < 0.001f)
                lookDirection = screens[i].forward;

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            float angleDiff = Quaternion.Angle(screens[i].rotation, targetRotation);
            if (angleDiff > 0.5f)
            {
                screens[i].rotation = Quaternion.Slerp(screens[i].rotation, targetRotation, Time.deltaTime * transitionSpeed);
            }
        }
    }

    void UpdateStandingMode(Vector3 currentHeadPos)
    {
        float distanceFromStablePosition = Vector3.Distance(currentHeadPos, stableHeadPosition);

        if (distanceFromStablePosition > stabilityRadius)
        {
            RecenterStandingPosition(currentHeadPos);
        }

        for (int i = 0; i < screens.Length; i++)
        {
            screens[i].position = Vector3.Lerp(screens[i].position, lockedScreenPosition, Time.deltaTime * transitionSpeed);

            // Rotate horizontally only to face user + keyboard tilt
            float angleDiff = Quaternion.Angle(screens[i].rotation, lockedScreenRotation);
            if (angleDiff > 0.5f)
            {
                screens[i].rotation = Quaternion.Slerp(screens[i].rotation, lockedScreenRotation, Time.deltaTime * transitionSpeed);
            }
        }
    }

    void EnterStandingMode(Vector3 headPos, Vector3 headForward, Vector3 headUp)
    {
        isInStandingMode = true;
        stableHeadPosition = headPos;

        lockedScreenPosition = headPos + headForward * standingOffset.z + headUp * standingOffset.y;

        Vector3 lookDirection = headPos - lockedScreenPosition;
        lookDirection.y = 0; // keep upright
        if (lookDirection.sqrMagnitude < 0.001f)
            lookDirection = Vector3.forward;

        Quaternion baseRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        Quaternion keyboardTilt = Quaternion.Euler(-keyboardAngle, 0, 0); // Negative tilt on X axis

        lockedScreenRotation = baseRotation * keyboardTilt;

        Debug.Log($"Entered standing mode - Screen locked at: {lockedScreenPosition}");
    }

    void RecenterStandingPosition(Vector3 newHeadPos)
    {
        stableHeadPosition = newHeadPos;

        Vector3 headForward = headTransform.forward;
        Vector3 headUp = headTransform.up;

        lockedScreenPosition = newHeadPos + headForward * standingOffset.z + headUp * standingOffset.y;

        Vector3 lookDirection = newHeadPos - lockedScreenPosition;
        lookDirection.y = 0;
        if (lookDirection.sqrMagnitude < 0.001f)
            lookDirection = Vector3.forward;

        Quaternion baseRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        Quaternion keyboardTilt = Quaternion.Euler(-keyboardAngle, 0, 0); // Negative tilt on X axis

        lockedScreenRotation = baseRotation * keyboardTilt;

        Debug.Log($"Recentered standing position - New locked position: {lockedScreenPosition}");
    }

    void ExitStandingMode()
    {
        isInStandingMode = false;
        Debug.Log("Exited standing mode - returning to sitting behavior");
    }

    public void ResetScreens()
    {
        for (int i = 0; i < screens.Length; i++)
        {
            verticalLiftProgress[i] = 0f;
        }
        isInStandingMode = false;
        InitializeScreens();
    }

    void OnDrawGizmos()
    {
        if (headTransform == null) return;

        Vector3 headPos = headTransform.position;
        Vector3 headForward = headTransform.forward;
        Vector3 headUp = headTransform.up;

        Gizmos.color = Color.blue;
        Vector3 frontPos = headPos + headForward * 1.2f + headUp * initialOffset.y;
        Gizmos.DrawWireSphere(frontPos, 0.1f);

        Gizmos.color = Color.green;
        Vector3 standingPos = headPos + headForward * standingOffset.z + headUp * standingOffset.y;
        Gizmos.DrawWireSphere(standingPos, 0.1f);

        if (isInStandingMode)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(stableHeadPosition, stabilityRadius);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawRay(headPos, headForward * 1f);
    }
}
