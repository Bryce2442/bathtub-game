using GONet;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PlayerKartController : MonoBehaviour
{
    [Header("Kart Stats")]
    public float acceleration = 30f;
    public float handling = 90f;
    public float speed = 20f;

    public enum WeightClass { Light, Medium, Heavy }
    public WeightClass weightClass = WeightClass.Medium;

    [Header("Driving State")]
    public bool canDrive = false;

    private float weightFactor;
    private float slopeFactor;
    private float handlingFactor;

    [Header("Drift & Boost")]
    public float driftMultiplier = 1.5f;
    public float boostMultiplier = 1.5f;
    public float boostDuration = 3f;

    [Header("Camera Settings")]
    public Transform playerCamera;

    // Third-person camera
    public float cameraDistance = 6f;
    public float cameraHeight = 3f;
    public float cameraLookHeight = 1f;
    public float cameraFollowSpeed = 8f;
    public float cameraRotationSpeed = 8f;

    public float tiltAmount = 5f;
    public float tiltSpeed = 5f;
    public float bobAmount = 0.05f;
    public float bobSpeed = 4f;

    [Header("Camera Shake")]
    public float collisionShakeIntensity = 0.05f;
    public float collisionShakeDuration = 0.25f;

    [Header("Engine & Drift Audio")]
    public AudioSource idleEngineAudio;
    public AudioSource engineAudio;
    public float engineMinPitch = 0.8f;
    public float engineMaxPitch = 2.0f;
    public float engineBoostPitch = 0.3f;

    [Header("Collision Effects")]
    public AudioSource collisionSound;
    public ParticleSystem collisionParticles;
    public float collisionSlowdownFactor = 0.6f;

    [Header("Boost Particle")]
    public ParticleSystem boostParticles;

    [Header("Stabilization Settings")]
    public float groundForce = 50f;

    [System.NonSerialized]
    public Rigidbody rb;

    private GONetParticipant gonetParticipant;
    private Collider col;

    private float moveInput;
    private float turnInput;

    private bool isBoosting = false;
    private bool isShaking = false;

    private Vector3 cameraShakeOffset = Vector3.zero;

    private float retainedSpeed = 0f;
    private float retainedSpeedDirection = 1f;

    private float reverseEngageSpeed = 0.5f;


    void Awake()
    {
        gonetParticipant = GetComponent<GONetParticipant>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        rb.centerOfMass = new Vector3(0, -0.6f, 0);

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        rb.collisionDetectionMode =
            CollisionDetectionMode.Continuous;

        switch (weightClass)
        {
            case WeightClass.Light:
                rb.mass = 0.8f;
                weightFactor = 0.8f;
                slopeFactor = 0.8f;
                handlingFactor = 0.8f;
                break;

            case WeightClass.Medium:
                rb.mass = 1.5f;
                weightFactor = 1f;
                slopeFactor = 1f;
                handlingFactor = 1f;
                break;

            case WeightClass.Heavy:
                rb.mass = 2.5f;
                weightFactor = 1.3f;
                slopeFactor = 1.2f;
                handlingFactor = 1.3f;
                break;
        }
    }


    // Offline/solo or locally owned kart.
    private bool IsLocallyControlled =>
        gonetParticipant == null ||
        gonetParticipant.IsLocallyControlled;


    void Update()
    {
        if (!IsLocallyControlled)
            return;

        ApplyInput();
        UpdateEngineSound();
    }


    void FixedUpdate()
    {
        if (!IsLocallyControlled)
            return;

        UpdatePosition();
        StabilizeKart();
    }


    void LateUpdate()
    {
        if (!IsLocallyControlled)
            return;

        HandleCamera();
    }


    void ApplyInput()
    {
        //moveInput = Input.GetAxis("Vertical");
        //turnInput = Input.GetAxis("Horizontal");

        moveInput = InputManager.PlayerMoveInput.y;
        turnInput = InputManager.PlayerMoveInput.x;

        //if (Input.GetKey(KeyCode.LeftShift))

        if (InputManager.DriftAction.IsPressed())
            turnInput *= driftMultiplier;

        //if (Input.GetKeyDown(KeyCode.Space) && !isBoosting)

        if (InputManager.BoostAction.WasPressedThisFrame() &&
            !isBoosting)
        {
            StartCoroutine(BoostCoroutine());
        }
    }


    void UpdateEngineSound()
    {
        if (engineAudio == null ||
            idleEngineAudio == null)
        {
            return;
        }

        float speedVal = rb.velocity.magnitude;

        bool isMoving =
            speedVal > 0.5f;

        float speedPercent =
            speedVal /
            (speed * weightFactor);

        engineAudio.pitch =
            Mathf.Lerp(
                engineMinPitch,
                engineMaxPitch,
                speedPercent
            );

        engineAudio.volume =
            Mathf.Lerp(
                0.2f,
                0.5f,
                speedPercent
            );

        if (isBoosting)
            engineAudio.pitch += engineBoostPitch;

        if (isMoving)
        {
            engineAudio.volume =
                Mathf.MoveTowards(
                    engineAudio.volume,
                    1f,
                    Time.deltaTime * 3f
                );

            idleEngineAudio.volume =
                Mathf.MoveTowards(
                    idleEngineAudio.volume,
                    0f,
                    Time.deltaTime * 3f
                );
        }
        else
        {
            idleEngineAudio.volume =
                Mathf.MoveTowards(
                    idleEngineAudio.volume,
                    0.8f,
                    Time.deltaTime * 3f
                );

            engineAudio.volume =
                Mathf.MoveTowards(
                    engineAudio.volume,
                    0f,
                    Time.deltaTime * 3f
                );
        }
    }


    void UpdatePosition()
    {
        if (!canDrive)
        {
            rb.velocity = Vector3.zero;
            return;
        }

        float currentAcceleration =
            acceleration *
            (1f / weightFactor);

        if (isBoosting)
            currentAcceleration *= boostMultiplier;

        float halfHeight =
            col != null
            ? col.bounds.extents.y
            : 1f;

        Vector3 rayStart =
            col != null
            ? col.bounds.center + Vector3.up * 0.1f
            : transform.position + Vector3.up * 0.1f;

        float rayLength =
            halfHeight + 0.5f;

        bool grounded =
            Physics.Raycast(
                rayStart,
                Vector3.down,
                out RaycastHit hit,
                rayLength
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
                    Time.fixedDeltaTime * 10f
                )
            );

            Vector3 slopeForward =
                Vector3.ProjectOnPlane(
                    transform.forward,
                    hit.normal
                ).normalized;


            // --------------------------
            //     FORWARD MOVEMENT
            // --------------------------

            if (moveInput > 0.1f)
            {
                rb.AddForce(
                    slopeForward *
                    currentAcceleration *
                    slopeFactor,
                    ForceMode.Acceleration
                );

                retainedSpeed =
                    rb.velocity.magnitude;

                retainedSpeedDirection = 1f;
            }


            // --------------------------
            //     BRAKING / REVERSE
            // --------------------------

            else if (moveInput < -0.1f)
            {
                if (Vector3.Dot(
                    rb.velocity,
                    transform.forward
                ) > reverseEngageSpeed)
                {
                    rb.velocity =
                        Vector3.MoveTowards(
                            rb.velocity,
                            Vector3.zero,
                            currentAcceleration *
                            Time.fixedDeltaTime
                        );

                    retainedSpeed =
                        rb.velocity.magnitude;

                    return;
                }

                rb.AddForce(
                    slopeForward *
                    moveInput *
                    currentAcceleration *
                    slopeFactor,
                    ForceMode.Acceleration
                );

                retainedSpeed =
                    rb.velocity.magnitude;

                retainedSpeedDirection = -1f;
            }

            else
            {
                // COASTING

                if (rb.velocity.magnitude <
                    retainedSpeed)
                {
                    Vector3 dir =
                        retainedSpeedDirection < 0
                        ? -transform.forward
                        : transform.forward;

                    rb.velocity =
                        dir * retainedSpeed;
                }
            }
        }

        else
        {
            // ------------------
            // MID-AIR FORWARD
            // ------------------

            if (moveInput > 0.1f)
            {
                rb.AddForce(
                    transform.forward *
                    currentAcceleration,
                    ForceMode.Acceleration
                );

                retainedSpeed =
                    rb.velocity.magnitude;

                retainedSpeedDirection = 1f;
            }


            // ------------------
            // MID-AIR REVERSE
            // ------------------

            else if (moveInput < -0.1f)
            {
                if (Vector3.Dot(
                    rb.velocity,
                    transform.forward
                ) > reverseEngageSpeed)
                {
                    rb.velocity =
                        Vector3.MoveTowards(
                            rb.velocity,
                            Vector3.zero,
                            currentAcceleration *
                            Time.fixedDeltaTime
                        );

                    retainedSpeed =
                        rb.velocity.magnitude;

                    return;
                }

                rb.AddForce(
                    transform.forward *
                    moveInput *
                    currentAcceleration,
                    ForceMode.Acceleration
                );

                retainedSpeed =
                    rb.velocity.magnitude;

                retainedSpeedDirection = -1f;
            }

            else
            {
                if (rb.velocity.magnitude <
                    retainedSpeed)
                {
                    Vector3 dir =
                        retainedSpeedDirection < 0
                        ? -transform.forward
                        : transform.forward;

                    rb.velocity =
                        dir * retainedSpeed;
                }
            }
        }


        // Clamp max speed

        float currentMaxSpeed =
            speed *
            weightFactor *
            (isBoosting ? boostMultiplier : 1f);

        if (rb.velocity.magnitude >
            currentMaxSpeed)
        {
            rb.velocity =
                rb.velocity.normalized *
                currentMaxSpeed;
        }


        // Turning

        float adjustedHandling =
            handling *
            handlingFactor;

        float turnAngle =
            turnInput *
            adjustedHandling *
            Time.fixedDeltaTime;

        rb.MoveRotation(
            rb.rotation *
            Quaternion.Euler(
                0,
                turnAngle,
                0
            )
        );


        // Keep velocity aligned with direction

        if (rb.velocity.magnitude > 0.1f)
        {
            Vector3 dir =
                retainedSpeedDirection < 0
                ? -transform.forward
                : transform.forward;

            rb.velocity =
                dir *
                rb.velocity.magnitude;
        }
    }


    void HandleCamera()
    {
        if (playerCamera == null)
            return;

        // Position behind and above kart.
        Vector3 targetPosition =
            transform.position
            - transform.forward * cameraDistance
            + Vector3.up * cameraHeight;

        // Camera bob.
        float speedVal =
            rb.velocity.magnitude;

        float bobOffset =
            Mathf.Sin(
                Time.time * bobSpeed
            ) *
            bobAmount *
            Mathf.Clamp01(
                speedVal / speed
            );

        targetPosition +=
            Vector3.up * bobOffset;

        // Collision shake.
        targetPosition +=
            cameraShakeOffset;

        playerCamera.position =
            Vector3.Lerp(
                playerCamera.position,
                targetPosition,
                Time.deltaTime *
                cameraFollowSpeed
            );


        // Look slightly above the kart.
        Vector3 lookTarget =
            transform.position +
            Vector3.up *
            cameraLookHeight;

        Vector3 lookDirection =
            lookTarget -
            playerCamera.position;

        if (lookDirection.sqrMagnitude >
            0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    lookDirection,
                    Vector3.up
                );

            // Keep your original turning tilt.
            float driftTilt =
                InputManager.DriftAction.IsPressed()
                ? 1.5f
                : 1f;

            float sideTilt =
                -turnInput *
                tiltAmount *
                driftTilt;

            targetRotation *=
                Quaternion.Euler(
                    0,
                    0,
                    sideTilt
                );

            playerCamera.rotation =
                Quaternion.Slerp(
                    playerCamera.rotation,
                    targetRotation,
                    Time.deltaTime *
                    cameraRotationSpeed
                );
        }
    }


    void StabilizeKart()
    {
        float halfHeight =
            col != null
            ? col.bounds.extents.y
            : 1f;

        Vector3 rayStart =
            col != null
            ? col.bounds.center
            : transform.position;

        float rayLength =
            halfHeight + 0.2f;

        if (!Physics.Raycast(
            rayStart,
            Vector3.down,
            rayLength))
        {
            rb.AddForce(
                Vector3.down *
                groundForce,
                ForceMode.Acceleration
            );
        }
    }


    IEnumerator BoostCoroutine()
    {
        isBoosting = true;

        if (boostParticles != null)
            boostParticles.Play();

        yield return new WaitForSeconds(
            boostDuration
        );

        isBoosting = false;

        if (boostParticles != null)
            boostParticles.Stop();
    }


    void OnCollisionEnter(
        Collision collision
    )
    {
        if (!IsLocallyControlled)
            return;

        if (collision.collider.CompareTag("Road") ||
            collision.collider.CompareTag("Terrain"))
        {
            return;
        }

        float impactForce =
            collision.relativeVelocity.magnitude;

        if (impactForce > 2f)
        {
            if (collisionSound != null)
                collisionSound.Play();

            if (collisionParticles != null)
                collisionParticles.Play();

            rb.velocity *=
                collisionSlowdownFactor;

            if (!isShaking &&
                playerCamera != null)
            {
                StartCoroutine(
                    CameraShake()
                );
            }
        }
    }


    IEnumerator CameraShake()
    {
        isShaking = true;

        float time = 0f;

        while (time <
               collisionShakeDuration)
        {
            float x =
                Random.Range(
                    -collisionShakeIntensity,
                    collisionShakeIntensity
                );

            float y =
                Random.Range(
                    -collisionShakeIntensity,
                    collisionShakeIntensity
                );

            cameraShakeOffset =
                new Vector3(
                    x,
                    y,
                    0f
                );

            time += Time.deltaTime;

            yield return null;
        }

        cameraShakeOffset =
            Vector3.zero;

        isShaking = false;
    }
}