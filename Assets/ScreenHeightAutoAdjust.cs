using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class ScreenHeightAutoAdjust : MonoBehaviour
{
    public Transform headReference;
    [Header("Screens")]
    public List<Transform> screenCandidates;
    private Transform screen; // active screen determined at runtime

    public float screenDistance = 0.5f;
    public float moveSpeed = 0.05f;
    public float transitionSpeed = 8f;
    public float rightOffsetDistance = 0.5f;
    public float horizontalMoveSpeed = 1f;
    public float delayBeforeRightMove = 0.5f;
    private Vector3 userPositionAtRightStart;
    private Vector3 centerStandingPosition;
    public float anglePerSecond = 0.1f;
    [Header("Ergonomics")]
    public float eyeOffset = 0.10f;
    public float ergonomicTilt = 10f;

    [Header("Rotation Settings")]
    public float tiltAngleX = 40f;
    public float maxHorizontalTiltY = 15f;
    private float targetTiltAngle = 0f;

    private Vector3 finalStandingPosition;
    private Vector3 targetPosition;

    private float sittingHeight = -1f;
    private float standingHeight = -1f;
    private bool isInitialized = false;
    private bool isUserStanding = false;
    private bool isAnimatingPrompt = false;

    private bool isMovingRight = false;
    private bool isMovingLeftBack = false;
    private bool waitingForUserToReachRight = false;
    private bool hasUserMovedAwayFromCenter = false;
    private Vector3 originalUserPosition;
    public string screenLogFilename = "screen_movement_log.csv";
    private string screenLogHeader = "Time;UserPosX;UserPosY;UserPos;ScreenPosX;ScreenPosY;ScreenPosZ;Distance;MoveSpeed;TargetTilt";

    private Quaternion defaultRotation;
    private bool shouldTilt = false;
    void Start()
    {
        defaultRotation = Quaternion.Euler(ergonomicTilt, 0f, 0f);
        Logger.LogLine(screenLogFilename, screenLogHeader, "", false); // Will only write header if needed

    }

    void Update()
    {
        UpdateActiveScreen();
        if (screen == null || !screen.gameObject.activeInHierarchy || headReference == null) return;

        if (!isInitialized && sittingHeight > 0 && standingHeight > 0)
            TryInitialize();

        float headY = headReference.position.y;
        bool wasStanding = isUserStanding;
        isUserStanding = (headY > sittingHeight + 0.3f);

        if (wasStanding != isUserStanding)
        {
            isAnimatingPrompt = false;
            if (isUserStanding)
                HandleStandingBehavior();
            else
                HandleSittingBehavior();
        }

        float speed = isAnimatingPrompt ? moveSpeed : transitionSpeed;

        if (!isMovingRight && !isMovingLeftBack && !waitingForUserToReachRight)
        {
            screen.position = Vector3.MoveTowards(screen.position, targetPosition, speed * Time.deltaTime);
            AnimateRotationDuringRise();
        }
        else if (isMovingRight)
        {
            shouldTilt = true;
            screen.position = Vector3.MoveTowards(screen.position, finalStandingPosition, horizontalMoveSpeed * Time.deltaTime);

            float moveProgress = 1f - Mathf.Clamp01(Vector3.Distance(centerStandingPosition, screen.position) / rightOffsetDistance);
            float currentTiltY = Mathf.Lerp(0f, -maxHorizontalTiltY, moveProgress);
            Quaternion targetRotation = Quaternion.Euler(0f, currentTiltY, 0f);
            screen.rotation = Quaternion.Slerp(screen.rotation, targetRotation, 5f * Time.deltaTime);

            if (Vector3.Distance(screen.position, finalStandingPosition) <= 0.01f)
            {
                isMovingRight = false;
                waitingForUserToReachRight = true;
                Debug.Log("🎯 Reached RIGHT position — waiting for user to be in front of screen");
            }
        }
        else if (isMovingLeftBack)
        {
            screen.position = Vector3.MoveTowards(screen.position, targetPosition, horizontalMoveSpeed * Time.deltaTime);

            float moveProgress = 1f - Mathf.Clamp01(Vector3.Distance(finalStandingPosition, screen.position) / rightOffsetDistance);
            float currentTiltY = Mathf.Lerp(maxHorizontalTiltY, 0f, moveProgress);
            Quaternion targetRotation = Quaternion.Euler(0f, currentTiltY, 0f);
            screen.rotation = Quaternion.Slerp(screen.rotation, targetRotation, 5f * Time.deltaTime);

            if (Vector3.Distance(screen.position, targetPosition) <= 0.01f)
            {
                isMovingLeftBack = false;
                Debug.Log("✅ Returned screen to CENTER");
            }
        }
        else if (!isMovingRight && !isMovingLeftBack && !waitingForUserToReachRight)
        {
            screen.rotation = Quaternion.Slerp(screen.rotation, defaultRotation, 5f * Time.deltaTime);
        }
        LogMovement(speed);
        CheckUserPositionForReturn();
    }

    void LogMovement(float moveSpeed)
    {
        float distance = Vector3.Distance(headReference.position, screen.position);
        string line = $"{Time.time:F2};" +
                      $"{headReference.position.x:F2};{headReference.position.y:F2};{headReference.position.z:F2};" +
                      $"{screen.position.x:F2};{screen.position.y:F2};{screen.position.z:F2};" +
                      $"{distance:F2};{moveSpeed:F2};{targetTiltAngle:F2}";

        Logger.LogLine(screenLogFilename, screenLogHeader, line, true); // ✅ true = append
    }


    void UpdateActiveScreen()
    {
        foreach (var candidate in screenCandidates)
        {
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                if (screen != candidate)
                {
                    screen = candidate;
                    isInitialized = false; // 👈 force re-initialization
                    Debug.Log("🎯 Active screen set to: " + screen.name);
                }
                return;
            }
        }
        screen = null;
    }


    void HandleStandingBehavior()
    {
        isMovingRight = false;
        isMovingLeftBack = false;
        waitingForUserToReachRight = false;
        hasUserMovedAwayFromCenter = false;

        StartCoroutine(StandingSequence());
    }

    void HandleSittingBehavior()
    {
        isMovingRight = false;
        isMovingLeftBack = false;
        waitingForUserToReachRight = false;
        hasUserMovedAwayFromCenter = false;

        StartCoroutine(SitThenPromptUp());
    }

    void CheckUserPositionForReturn()
    {
        if (!waitingForUserToReachRight || !isUserStanding || isAnimatingPrompt)
            return;

        if (Vector3.Distance(screen.position, finalStandingPosition) > 0.05f)
            return;

        Vector3 userXZ = new Vector3(headReference.position.x, 0f, headReference.position.z);
        Vector3 originalXZ = new Vector3(originalUserPosition.x, 0f, originalUserPosition.z);
        Vector3 screenXZ = new Vector3(screen.position.x, 0f, screen.position.z);

        float distanceFromOriginal = Vector3.Distance(userXZ, originalXZ);
        Vector3 toUser = (userXZ - screenXZ).normalized;
        float lateralOffset = Vector3.Dot(screen.right, toUser);
        float forwardAlignment = Vector3.Dot(screen.forward, toUser);

        if (!hasUserMovedAwayFromCenter && distanceFromOriginal > 0.3f)
        {
            hasUserMovedAwayFromCenter = true;
        }

        if (hasUserMovedAwayFromCenter && Mathf.Abs(lateralOffset) <= 0.2f && forwardAlignment < -0.8f)
        {
            shouldTilt = false;
            ReturnScreenToCenter();
        }
    }

    void ReturnScreenToCenter()
    {
        waitingForUserToReachRight = false;
        isMovingLeftBack = true;
        targetPosition = centerStandingPosition;
    }

    void AnimateRotationDuringRise()
    {
        float currentX = screen.rotation.eulerAngles.x;

        // Convert angle > 180° to negative range to allow proper directional rotation
        if (currentX > 180f) currentX -= 360f;

        // Gradually rotate toward targetTiltAngle at a fixed speed
        float step = anglePerSecond * Time.deltaTime;
        float newX = Mathf.MoveTowards(currentX, targetTiltAngle, step);

        screen.rotation = Quaternion.Euler(newX, 0f, 0f);
    }


    void SetScreenPosition(float height, bool applyRightOffset = false, bool fromInit = false)
    {
        Vector3 forward = new Vector3(headReference.forward.x, 0f, headReference.forward.z).normalized;
        Vector3 basePos = headReference.position + forward * screenDistance;
        basePos.y = height - eyeOffset;
        targetPosition = basePos;

        if (isAnimatingPrompt)
            targetTiltAngle = tiltAngleX;
        else
            targetTiltAngle = ergonomicTilt;
    }

    public void SetSittingHeight(float height)
    {
        sittingHeight = height;
        TryInitialize();
    }

    public void SetStandingHeight(float height)
    {
        standingHeight = height;
        TryInitialize();
    }

    void TryInitialize()
    {
        if (sittingHeight > 0 && standingHeight > 0 && !isInitialized)
        {
            isInitialized = true;

            float headY = headReference.position.y;
            isUserStanding = (headY > sittingHeight + 0.3f);

            if (isUserStanding)
            {
                SetScreenPosition(standingHeight);
                screen.position = targetPosition;
            }
            else
            {
                SetScreenPosition(sittingHeight);
                screen.position = targetPosition;
                isAnimatingPrompt = true;
                SetScreenPosition(standingHeight, applyRightOffset: false, fromInit: true);
            }
        }
    }

    IEnumerator StandingSequence()
    {
        originalUserPosition = new Vector3(headReference.position.x, 0f, headReference.position.z);

        SetScreenPosition(standingHeight, applyRightOffset: false);
        while (Vector3.Distance(screen.position, targetPosition) > 0.01f)
            yield return null;

        Vector3 forward = new Vector3(headReference.forward.x, 0f, headReference.forward.z).normalized;
        Vector3 right = new Vector3(headReference.right.x, 0f, headReference.right.z).normalized;

        Vector3 basePos = headReference.position + forward * screenDistance;
        basePos.y = standingHeight - eyeOffset; // ✅ Apply correct ergonomic offset

        centerStandingPosition = basePos;
        finalStandingPosition = basePos + right * rightOffsetDistance;
        yield return new WaitForSeconds(delayBeforeRightMove);
        isMovingRight = true;
    }

    IEnumerator SitThenPromptUp()
    {
        isAnimatingPrompt = false;
        SetScreenPosition(sittingHeight);

        while (Vector3.Distance(screen.position, targetPosition) > 0.01f)
            yield return null;

        yield return new WaitForSeconds(0.3f);

        isAnimatingPrompt = true;
        SetScreenPosition(standingHeight);
    }
}