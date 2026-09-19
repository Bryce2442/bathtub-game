using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using System.Collections;
using GONet;

[RequireComponent(typeof(Rigidbody))]
public class OpponentKartAI : MonoBehaviour
{
    [Header("Spline Track")]
    public SplineContainer trackSpline;

    [Header("Look-Ahead Settings")]
    [Tooltip("Base distance (meters) ahead on the spline the AI steers toward.")]
    public float lookAheadDistance = 15f;

    [Tooltip("Extra look-ahead multiplier applied at full speed (scales with current speed ratio).")]
    public float speedLookAheadScale = 0.5f;

    [Header("AI Movement Settings")]
    public float acceleration = 20f;
    public float maxSpeed = 15f;

    [Header("Boost")]
    public float boostMultiplier = 1.5f;
    public float boostDuration = 3f;
    public float boostRandomCooldown = 9f;

    [Header("Curvature Braking")]
    [Tooltip("How aggressively the AI brakes for curves. Higher = more braking.")]
    public float curvatureBrakeStrength = 5f;

    [Tooltip("Minimum speed the AI will maintain in tight curves.")]
    public float minCurveSpeed = 6f;

    [Header("Slope Settings (match player)")]
    public float slopeFactor = 1f;
    public float groundForce = 50f;

    [Header("AI Weight")]
    public float AIWeight;

    [Header("Kart Visuals")]
    public MeshRenderer bathtubRenderer;

    [Header("Race Control")]
    public bool canDrive = false;

    [Header("Stuck Phasing")]
    [Tooltip("Speed below this value counts as being stuck.")]
    public float stuckSpeedThreshold = 1f;

    [Tooltip("How long the kart must be moving slowly before phasing.")]
    public float stuckTimeThreshold = 1.5f;

    [Tooltip("How long the kart can phase through other AI karts.")]
    public float phaseDuration = 2f;

    [Space]
    [Header("Runtime Set Props")]
    public bool IsBoosting = false;
    public float RemainingBoostCooldown = 0f;
    public bool IsPhasing = false;

    public float ScaledMaxSpeed =>
        maxSpeed * (IsBoosting ? boostMultiplier : 1f);

    GONetParticipant participant;
    Rigidbody rb;
    float currentT;

    float stuckTimer = 0f;

    Collider[] kartColliders;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        rb.collisionDetectionMode =
            CollisionDetectionMode.Continuous;

        participant =
            GetComponent<GONetParticipant>();

        kartColliders =
            GetComponentsInChildren<Collider>();

        AIWeight =
            Random.Range(0.8f, 1.3f);

        RandomizeBodyColor();

        SetSplineTrack(
            RaceManager.Instance.TrackSpline
        );

        // Start with an initial random cooldown.
        RemainingBoostCooldown =
            Random.Range(0, boostRandomCooldown);
    }

    void OnEnable()
    {
        Debug.Log(
            $"Enabling AI Kart: {gameObject.name}"
        );

        RaceManager.Instance.AddKartAI(
            this,
            true
        );
    }

    void OnDisable()
    {
        RaceManager.Instance.AddKartAI(
            this,
            false
        );
    }

    void Update()
    {
        if (!canDrive || !participant.IsMine)
            return;

        RemainingBoostCooldown =
            Mathf.Max(
                0f,
                RemainingBoostCooldown - Time.deltaTime
            );

        if (!IsBoosting &&
            RemainingBoostCooldown == 0)
        {
            Boost();
        }
    }

    void FixedUpdate()
    {
        if (!canDrive)
        {
            rb.velocity =
                Vector3.Lerp(
                    rb.velocity,
                    Vector3.zero,
                    Time.deltaTime
                );

            stuckTimer = 0f;

            return;
        }

        // Check if the AI is stuck.
        CheckIfStuck();

        if (trackSpline != null)
            HandleSplineMovement();
        else
            HandleSlopeMovement();
    }

    void RandomizeBodyColor()
    {
        if (bathtubRenderer != null)
        {
            Color randomColor =
                new Color(
                    Random.Range(0.2f, 1f),
                    Random.Range(0.2f, 1f),
                    Random.Range(0.2f, 1f)
                );

            bathtubRenderer.material =
                new Material(
                    bathtubRenderer.material
                );

            bathtubRenderer.material.color =
                randomColor;
        }
    }

    public void SetSplineTrack(
        SplineContainer container
    )
    {
        trackSpline = container;
    }

    // Generic AI racer movement algorithm that follows a spline path defined
    // in the scene using Unity's spline utility.
    void HandleSplineMovement()
    {
        var spline =
            trackSpline.Spline;

        // Project current position onto spline.
        float3 localPos =
            trackSpline.transform
            .InverseTransformPoint(
                transform.position
            );

        SplineUtility.GetNearestPoint(
            spline,
            localPos,
            out float3 nearestLocal,
            out float t
        );

        currentT = t;

        // Compute look-ahead target.
        float splineLength =
            spline.GetLength();

        if (splineLength < 0.01f)
            return;

        float speedFraction =
            rb.velocity.magnitude /
            Mathf.Max(
                ScaledMaxSpeed,
                0.01f
            );

        float totalLookAhead =
            lookAheadDistance +
            speedFraction *
            speedLookAheadScale *
            lookAheadDistance;

        float tOffset =
            totalLookAhead /
            splineLength;

        float lookAheadT =
            (currentT + tOffset) % 1f;

        float3 lookAheadLocal =
            spline.EvaluatePosition(
                lookAheadT
            );

        Vector3 targetPos =
            trackSpline.transform
            .TransformPoint(
                lookAheadLocal
            );

        // Curvature-based speed control.
        float curvature =
            spline.EvaluateCurvature(
                lookAheadT
            );

        float curveSpeedFactor =
            1f /
            (
                1f +
                curvature *
                curvatureBrakeStrength
            );

        float targetSpeed =
            Mathf.Lerp(
                minCurveSpeed,
                ScaledMaxSpeed,
                curveSpeedFactor
            );

        // Account for vertical slopes.
        RaycastHit hit;

        bool grounded =
            Physics.Raycast(
                transform.position +
                Vector3.up * 0.2f,
                Vector3.down,
                out hit,
                1.5f
            );

        Vector3 desiredDirection;

        if (grounded)
        {
            Quaternion slopeRotation =
                Quaternion.FromToRotation(
                    transform.up,
                    hit.normal
                ) *
                transform.rotation;

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    slopeRotation,
                    Time.fixedDeltaTime * 8f
                )
            );

            Vector3 slopeForward =
                Vector3.ProjectOnPlane(
                    (
                        targetPos -
                        transform.position
                    ).normalized,
                    hit.normal
                ).normalized;

            desiredDirection =
                slopeForward;
        }
        else
        {
            desiredDirection =
                (
                    targetPos -
                    transform.position
                ).normalized;
        }

        if (desiredDirection.sqrMagnitude > 0.01f &&
            grounded)
        {
            Quaternion directionalRotation =
                Quaternion.LookRotation(
                    desiredDirection,
                    hit.normal
                );

            Quaternion finalRotation =
                Quaternion.Slerp(
                    rb.rotation,
                    directionalRotation,
                    Time.fixedDeltaTime * 4f
                );

            rb.MoveRotation(
                finalRotation
            );
        }

        // Acceleration.
        float currentAcceleration =
            acceleration *
            (1f / AIWeight);

        rb.AddForce(
            desiredDirection *
            currentAcceleration,
            ForceMode.Acceleration
        );

        // Speed limiting based on AI scene settings.
        if (rb.velocity.magnitude > targetSpeed)
        {
            rb.velocity =
                rb.velocity.normalized *
                targetSpeed;
        }
    }

    // Check if the AI has stopped moving.
    void CheckIfStuck()
    {
        if (IsPhasing)
            return;

        if (rb.velocity.magnitude <
            stuckSpeedThreshold)
        {
            stuckTimer +=
                Time.fixedDeltaTime;

            if (stuckTimer >=
                stuckTimeThreshold)
            {
                StartCoroutine(
                    PhaseThroughAI()
                );

                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    // Temporarily ignore collisions with other AI karts.
    IEnumerator PhaseThroughAI()
    {
        IsPhasing = true;

        OpponentKartAI[] otherKarts =
            FindObjectsOfType<OpponentKartAI>();

        foreach (OpponentKartAI otherKart in otherKarts)
        {
            if (otherKart == this)
                continue;

            Collider[] otherColliders =
                otherKart.GetComponentsInChildren<Collider>();

            foreach (Collider myCollider in kartColliders)
            {
                foreach (Collider otherCollider in otherColliders)
                {
                    if (myCollider != null &&
                        otherCollider != null)
                    {
                        Physics.IgnoreCollision(
                            myCollider,
                            otherCollider,
                            true
                        );
                    }
                }
            }
        }

        Debug.Log(
            $"{gameObject.name} is stuck - temporarily phasing through AI."
        );

        yield return new WaitForSeconds(
            phaseDuration
        );

        // Turn collisions back on.
        foreach (OpponentKartAI otherKart in otherKarts)
        {
            if (otherKart == null ||
                otherKart == this)
            {
                continue;
            }

            Collider[] otherColliders =
                otherKart.GetComponentsInChildren<Collider>();

            foreach (Collider myCollider in kartColliders)
            {
                foreach (Collider otherCollider in otherColliders)
                {
                    if (myCollider != null &&
                        otherCollider != null)
                    {
                        Physics.IgnoreCollision(
                            myCollider,
                            otherCollider,
                            false
                        );
                    }
                }
            }
        }

        IsPhasing = false;

        Debug.Log(
            $"{gameObject.name} stopped phasing."
        );
    }

    public void Boost()
    {
        if (IsBoosting ||
            RemainingBoostCooldown > 0)
        {
            return;
        }

        RemainingBoostCooldown =
            Random.Range(
                boostRandomCooldown * 0.5f,
                boostRandomCooldown
            );

        StartCoroutine(
            BoostCoroutine()
        );
    }

    IEnumerator BoostCoroutine()
    {
        IsBoosting = true;

        yield return new WaitForSeconds(
            boostDuration
        );

        IsBoosting = false;
    }

    void HandleSlopeMovement()
    {
        RaycastHit hit;

        bool grounded =
            Physics.Raycast(
                transform.position +
                Vector3.up * 0.2f,
                Vector3.down,
                out hit,
                1.5f
            );

        if (grounded)
        {
            Quaternion slopeRotation =
                Quaternion.FromToRotation(
                    transform.up,
                    hit.normal
                ) *
                transform.rotation;

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    slopeRotation,
                    Time.fixedDeltaTime * 8f
                )
            );

            Vector3 slopeForward =
                Vector3.ProjectOnPlane(
                    transform.forward,
                    hit.normal
                ).normalized;

            float slopeAngle =
                Vector3.Angle(
                    hit.normal,
                    Vector3.up
                );

            float adjustment = 1f;

            if (slopeAngle > 0.1f)
            {
                if (Vector3.Dot(
                    slopeForward,
                    Vector3.up
                ) < 0)
                {
                    adjustment =
                        1f / slopeFactor;
                }
                else
                {
                    adjustment =
                        slopeFactor;
                }
            }

            float currentAcceleration =
                acceleration *
                (1f / AIWeight);

            Vector3 force =
                slopeForward *
                currentAcceleration *
                adjustment;

            rb.AddForce(
                force,
                ForceMode.Acceleration
            );

            if (rb.velocity.magnitude >
                ScaledMaxSpeed)
            {
                rb.velocity =
                    rb.velocity.normalized *
                    ScaledMaxSpeed;
            }
        }
        else
        {
            rb.AddForce(
                Vector3.down *
                groundForce *
                0.5f,
                ForceMode.Acceleration
            );
        }
    }
}