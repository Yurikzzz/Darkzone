using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    private struct ShakeImpulse
    {
        public float intensity;
        public float duration;
        public float elapsed;
        public Vector2 direction;
        public float seedX;
        public float seedY;
    }

    public static CameraFollow Instance { get; private set; }

    [Header("Target")]
    public Transform target;

    [Header("Follow Settings")]
    [SerializeField] private bool smoothFollow = false;
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Screen Shake")]
    [Tooltip("Master toggle for camera screen shake.")]
    [SerializeField] private bool enableScreenShake = true;

    [Tooltip("Global multiplier applied to all screen shake intensities.")]
    [Min(0f)]
    [SerializeField] private float shakeMultiplier = 1f;

    [Tooltip("Oscillation frequency for Perlin-based screen shake.")]
    [Min(1f)]
    [SerializeField] private float shakeFrequency = 32f;

    [Tooltip("Maximum world-space camera offset allowed when multiple shakes overlap.")]
    [Min(0f)]
    [SerializeField] private float maxShakeOffset = 0.35f;

    private Vector3 currentVelocity;
    private Vector3 appliedShakeOffset;
    private float shakeNoiseTime;
    private float seedCounter;
    private readonly List<ShakeImpulse> activeImpulses = new List<ShakeImpulse>(8);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticInstance()
    {
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private void OnDisable()
    {
        if (appliedShakeOffset != Vector3.zero)
        {
            transform.position -= appliedShakeOffset;
            appliedShakeOffset = Vector3.zero;
        }
        activeImpulses.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Triggers an omnidirectional screen shake on the active CameraFollow instance.
    /// </summary>
    public static void Shake(float intensity, float duration = 0.15f)
    {
        CameraFollow cam = GetActiveInstance();
        if (cam != null)
            cam.TriggerShake(intensity, duration);
    }

    /// <summary>
    /// Triggers a screen shake biased along a world-space direction (e.g. opposite gun fire).
    /// </summary>
    public static void ShakeDirectional(Vector2 direction, float intensity, float duration = 0.12f)
    {
        CameraFollow cam = GetActiveInstance();
        if (cam != null)
            cam.TriggerShake(intensity, duration, direction);
    }

    /// <summary>
    /// Adds a screen shake impulse to this camera. Multiple simultaneous shakes blend cleanly and are clamped.
    /// </summary>
    public void TriggerShake(float intensity, float duration = 0.15f, Vector2 direction = default)
    {
        if (!enableScreenShake || intensity <= 0f || duration <= 0f || shakeMultiplier <= 0f)
            return;

        seedCounter += 17.31f;
        activeImpulses.Add(new ShakeImpulse
        {
            intensity = intensity,
            duration = duration,
            elapsed = 0f,
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero,
            seedX = seedCounter,
            seedY = seedCounter + 53.7f
        });
    }

    private static CameraFollow GetActiveInstance()
    {
        if (Instance != null) return Instance;
        Instance = FindFirstObjectByType<CameraFollow>();
        return Instance;
    }

    private void LateUpdate()
    {
        // Strip previous frame's shake before follow / SmoothDamp so shake never feeds into velocity or drifts
        Vector3 basePosition = transform.position - appliedShakeOffset;

        if (target != null)
        {
            Vector3 targetPosition = target.position + offset;

            if (smoothFollow && smoothTime > 0f)
            {
                basePosition = Vector3.SmoothDamp(basePosition, targetPosition, ref currentVelocity, smoothTime);
            }
            else
            {
                basePosition = targetPosition;
            }
        }

        appliedShakeOffset = EvaluateShakeOffset(Time.deltaTime);
        transform.position = basePosition + appliedShakeOffset;
    }

    private Vector3 EvaluateShakeOffset(float dt)
    {
        if (!enableScreenShake || activeImpulses.Count == 0 || dt <= 0f)
        {
            if (activeImpulses.Count > 0 && !enableScreenShake)
                activeImpulses.Clear();
            return Vector3.zero;
        }

        shakeNoiseTime += dt * shakeFrequency;
        Vector2 totalOffset = Vector2.zero;

        for (int i = activeImpulses.Count - 1; i >= 0; i--)
        {
            ShakeImpulse imp = activeImpulses[i];
            imp.elapsed += dt;

            if (imp.elapsed >= imp.duration)
            {
                activeImpulses.RemoveAt(i);
                continue;
            }

            activeImpulses[i] = imp;

            float remaining = 1f - Mathf.Clamp01(imp.elapsed / imp.duration);
            float falloff = remaining * remaining;
            float mag = imp.intensity * falloff;

            float nx = Mathf.PerlinNoise(imp.seedX + shakeNoiseTime, 0.37f) * 2f - 1f;
            float ny = Mathf.PerlinNoise(0.73f, imp.seedY + shakeNoiseTime) * 2f - 1f;
            Vector2 jitter = new Vector2(nx, ny);

            if (imp.direction != Vector2.zero)
            {
                // Blend directional kick with subtle perpendicular jitter
                Vector2 directionalKick = imp.direction * (mag * 0.65f);
                totalOffset += directionalKick + jitter * (mag * 0.55f);
            }
            else
            {
                totalOffset += jitter * mag;
            }
        }

        totalOffset *= shakeMultiplier;
        if (maxShakeOffset > 0f && totalOffset.sqrMagnitude > maxShakeOffset * maxShakeOffset)
            totalOffset = totalOffset.normalized * maxShakeOffset;

        return new Vector3(totalOffset.x, totalOffset.y, 0f);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
