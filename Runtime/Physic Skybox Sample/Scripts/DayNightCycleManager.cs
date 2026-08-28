using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public sealed class DayNightCycleManager : MonoBehaviour
{
    // ============================================================
    // ASTRONOMICAL SYSTEM
    // ============================================================

    [Header("Astronomical System")]
    [SerializeField]
    private AstronomicalTimeSystem astronomicalSystem;


    // ============================================================
    // SUN
    // ============================================================

    [Header("Sun")]

    [SerializeField]
    private Light sunLight;

    [SerializeField]
    private Gradient sunColor;

    /*
     * X = SunDayFactor
     *
     * 0 = -6°
     * 1 = +6° o superior
     */

    [SerializeField]
    private AnimationCurve sunIntensity =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);


    // ============================================================
    // MOON
    // ============================================================

    [Header("Moon")]

    [SerializeField]
    private Light moonLight;

    [SerializeField]
    private Gradient moonColor;

    /*
     * X = Moon altitude normalizada
     *
     * -6° = 0
     * +6° = 1
     */

    [SerializeField]
    private AnimationCurve moonIntensity =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);


    // ============================================================
    // TWILIGHT
    // ============================================================

    [Header("Twilight")]

    [Tooltip("Solar altitude where twilight begins.")]
    [SerializeField]
    private float twilightStartAltitude = -6f;

    [Tooltip("Solar altitude where astronomical twilight ends.")]
    [SerializeField]
    private float twilightEndAltitude = -18f;

    [SerializeField]
    private AnimationCurve twilightIntensity =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);


    // ============================================================
    // AMBIENT LIGHT
    // ============================================================

    [Header("Ambient Light")]

    /*
     * Ambient COLOR follows the Sun.
     *
     * It is intentionally independent from the Moon.
     */

    [SerializeField]
    private Gradient ambientColor;

    /*
     * X = SunDayFactor
     */

    [SerializeField]
    private AnimationCurve ambientIntensity =
        new AnimationCurve(
            new Keyframe(0f, 0.10f),
            new Keyframe(0.15f, 0.11f),
            new Keyframe(0.35f, 0.16f),
            new Keyframe(0.55f, 0.30f),
            new Keyframe(0.75f, 0.60f),
            new Keyframe(1f, 1f)
        );

    /*
     * Prevents the environment from becoming completely black.
     */

    [SerializeField]
    [Min(0f)]
    private float minimumAmbientIntensity = 0.08f;

    /*
     * Moon can slightly increase ambient brightness,
     * but does not influence ambient COLOR.
     */

    [SerializeField]
    [Range(0f, 1f)]
    private float moonAmbientContribution = 0.10f;


    // ============================================================
    // FOG
    // ============================================================

    [Header("Fog")]

    /*
     * Fog is controlled ONLY by SunDayFactor.
     *
     * 0 = -6°
     * 1 = +6°
     */

    [SerializeField]
    private Gradient fogColor;

    [SerializeField]
    private AnimationCurve fogDensity =
        AnimationCurve.Linear(0f, 0.02f, 1f, 0.01f);


    // ============================================================
    // URP VOLUME
    // ============================================================

    [Header("URP Volume")]

    [SerializeField]
    private Volume globalVolume;


    // ============================================================
    // COLOR ADJUSTMENTS
    // ============================================================

    [Header("Color Adjustments")]

    [SerializeField]
    private AnimationCurve postExposure =
        AnimationCurve.Linear(0f, -1f, 1f, 0f);

    [SerializeField]
    private Gradient colorFilter;

    [SerializeField]
    private AnimationCurve saturation =
        AnimationCurve.Linear(0f, -20f, 1f, 0f);

    [SerializeField]
    private AnimationCurve contrast =
        AnimationCurve.Linear(0f, 10f, 1f, 0f);


    // ============================================================
    // BLOOM
    // ============================================================

    [Header("Bloom")]

    [SerializeField]
    private AnimationCurve bloomIntensity =
        AnimationCurve.Linear(0f, 0.2f, 1f, 0.5f);


    // ============================================================
    // VIGNETTE
    // ============================================================

    [Header("Vignette")]

    [SerializeField]
    private AnimationCurve vignetteIntensity =
        AnimationCurve.Linear(0f, 0.3f, 1f, 0.1f);


    // ============================================================
    // MOON CURVE RANGE
    // ============================================================

    [Header("Moon Curve Range")]

    [Tooltip("Moon altitude mapped to curve 0.")]
    [SerializeField]
    private float moonMinAltitude = -6f;

    [Tooltip("Moon altitude mapped to curve 1.")]
    [SerializeField]
    private float moonMaxAltitude = 6f;


    // ============================================================
    // LIGHT INTERSECTION
    // ============================================================

    [Header("Light Intersection")]

    /*
     * We do NOT use independent Sun/Moon thresholds anymore.
     *
     * The lights exchange control when their target intensities
     * become equal.
     */

    [Tooltip("Maximum intensity difference considered an intersection.")]
    [SerializeField]
    [Min(0.000001f)]
    private float intersectionTolerance = 0.001f;

    /*
     * Prevents an immediate second exchange if the two values
     * remain extremely close for several frames.
     */

    [Tooltip("Minimum time between two light ownership changes.")]
    [SerializeField]
    [Min(0f)]
    private float minimumHandoffInterval = 0.25f;


    // ============================================================
    // LIGHT SMOOTHING
    // ============================================================

    [Header("Light Smoothing")]

    [Tooltip("How quickly light intensity follows its target.")]
    [SerializeField]
    [Min(0.01f)]
    private float lightIntensitySmoothTime = 2.0f;

    [Tooltip("How quickly light color follows its target.")]
    [SerializeField]
    [Min(0.01f)]
    private float lightColorSmoothTime = 2.0f;


    // ============================================================
    // INTERNAL REFERENCES
    // ============================================================

    private ColorAdjustments colorAdjustments;
    private Bloom bloom;
    private Vignette vignette;


    // ============================================================
    // LIGHT STATE
    // ============================================================

    private enum ActiveLight
    {
        None,
        Sun,
        Moon
    }

    private ActiveLight activeLight =
        ActiveLight.None;


    /*
     * Actual visual state of the currently active light.
     */

    private float currentLightIntensity;
    private float lightIntensityVelocity;

    private Color currentLightColor =
        Color.black;


    /*
     * Previous comparison value.
     *
     * This lets us detect an actual crossing rather than simply
     * checking whether the two values happen to be close.
     */

    private float previousLightDifference =
        float.NaN;


    /*
     * Prevents immediate back-and-forth ownership changes.
     */

    private float lastHandoffTime =
        -Mathf.Infinity;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        SetupVolume();

        DisableAllLights();
    }


    private void OnEnable()
    {
        SetupVolume();

        ForceUpdate();
    }


    private void Start()
    {
        SetupVolume();

        ForceUpdate();
    }


    private void Update()
    {
        UpdateDayNight();
    }


    // ============================================================
    // MAIN UPDATE
    // ============================================================

    private void UpdateDayNight()
    {
        if (astronomicalSystem == null)
            return;


        float sunAltitude =
            (float)astronomicalSystem.SunAltitude;

        float moonAltitude =
            (float)astronomicalSystem.MoonAltitude;

        float sunDayFactor =
            astronomicalSystem.SunDayFactor;

        float sunVisibility =
            astronomicalSystem.SunVisibility;


        float twilightFactor =
            CalculateTwilightFactor(
                sunAltitude);

        float moonFactor =
            CalculateMoonFactor(
                moonAltitude);


        /*
         * Calculate BOTH lights regardless of which one is active.
         *
         * This is essential because we need to know where their
         * curves intersect.
         */

        float sunValue =
            CalculateSunIntensity(
                sunDayFactor,
                sunVisibility);

        float moonValue =
            CalculateMoonIntensity(
                moonAltitude);


        /*
         * Decide whether the ownership of the directional light
         * needs to change.
         */

        UpdateLightIntersection(
            sunValue,
            moonValue,
            moonAltitude);


        /*
         * Update only the currently active light.
         */

        UpdateActiveLight(
            sunValue,
            moonValue,
            sunDayFactor,
            moonFactor);


        /*
         * Ambient remains independent from light ownership.
         */

        UpdateAmbient(
            sunDayFactor,
            twilightFactor,
            moonFactor);


        /*
         * Fog remains exclusively controlled by SunDayFactor.
         */

        UpdateFog(
            sunDayFactor);


        UpdatePostProcessing(
            sunDayFactor);
    }


    // ============================================================
    // LIGHT INTERSECTION
    // ============================================================

    private void UpdateLightIntersection(
        float sunValue,
        float moonValue,
        float moonAltitude)
    {
        /*
         * If there is no active light yet, choose the source
         * that currently represents the correct astronomical state.
         */

        if (activeLight == ActiveLight.None)
        {
            if (sunValue > moonValue)
            {
                ActivateLight(
                    ActiveLight.Sun);
            }
            else if (
                moonAltitude > moonMinAltitude &&
                moonValue > sunValue)
            {
                ActivateLight(
                    ActiveLight.Moon);
            }
            else
            {
                /*
                 * At initialization, prefer the Sun around the
                 * exact intersection if both values are tiny.
                 */

                ActivateLight(
                    ActiveLight.Sun);
            }

            previousLightDifference =
                sunValue - moonValue;

            return;
        }


        /*
         * Current difference:
         *
         * positive = Sun stronger
         * negative = Moon stronger
         */

        float currentDifference =
            sunValue - moonValue;


        /*
         * First evaluation.
         */

        if (float.IsNaN(
            previousLightDifference))
        {
            previousLightDifference =
                currentDifference;

            return;
        }


        /*
         * Don't allow an immediate second handoff.
         */

        bool canHandoff =
            Time.realtimeSinceStartup -
            lastHandoffTime >=
            minimumHandoffInterval;


        if (!canHandoff)
        {
            previousLightDifference =
                currentDifference;

            return;
        }


        /*
         * Detect a real crossing.
         *
         * Example:
         *
         * Frame N:
         * Sun - Moon = +0.002
         *
         * Frame N+1:
         * Sun - Moon = -0.001
         *
         * The curves crossed between these frames.
         */

        bool crossed =
            Mathf.Sign(
                previousLightDifference) !=
            Mathf.Sign(
                currentDifference);


        /*
         * If the values are already extremely close, we also
         * consider this an intersection.
         */

        bool almostEqual =
            Mathf.Abs(
                currentDifference) <=
            intersectionTolerance;


        if (crossed || almostEqual)
        {
            /*
             * At the intersection, both lights represent nearly
             * the same illumination.
             *
             * Exchange ownership here.
             */

            ActiveLight nextLight;


            if (currentDifference >= 0f)
            {
                nextLight =
                    ActiveLight.Sun;
            }
            else
            {
                nextLight =
                    ActiveLight.Moon;
            }


            /*
             * Only change if ownership really needs to change.
             */

            if (nextLight != activeLight)
            {
                ActivateLight(
                    nextLight);

                lastHandoffTime =
                    Time.realtimeSinceStartup;
            }
        }


        previousLightDifference =
            currentDifference;
    }


    // ============================================================
    // ACTIVATE LIGHT
    // ============================================================

    private void ActivateLight(
        ActiveLight newLight)
    {
        if (newLight == activeLight)
            return;


        /*
         * Capture the exact visual state currently visible
         * BEFORE disabling the old light.
         */

        CaptureCurrentLightState();


        /*
         * Disable both lights.
         *
         * This guarantees that there are never two directional
         * lights active simultaneously.
         */

        DisableAllLights();


        activeLight =
            newLight;


        Light newActiveLight =
            GetActiveLight();


        if (newActiveLight == null)
        {
            activeLight =
                ActiveLight.None;

            return;
        }


        /*
         * CRITICAL:
         *
         * The new light starts at the EXACT intensity and color
         * of the old light.
         *
         * Therefore:
         *
         * Frame N:
         * OldLight = X
         *
         * Frame N+1:
         * NewLight = X
         *
         * There is no illumination discontinuity.
         */

        newActiveLight.intensity =
            currentLightIntensity;

        newActiveLight.color =
            currentLightColor;

        newActiveLight.enabled =
            true;


        EnforceSingleActiveLight();
    }


    // ============================================================
    // ACTIVE LIGHT UPDATE
    // ============================================================

    private void UpdateActiveLight(
        float sunValue,
        float moonValue,
        float sunDayFactor,
        float moonFactor)
    {
        Light light =
            GetActiveLight();


        if (light == null)
            return;


        float targetIntensity;
        Color targetColor;


        if (activeLight == ActiveLight.Sun)
        {
            targetIntensity =
                sunValue;

            targetColor =
                sunColor.Evaluate(
                    sunDayFactor);
        }
        else if (activeLight == ActiveLight.Moon)
        {
            targetIntensity =
                moonValue;

            targetColor =
                moonColor.Evaluate(
                    moonFactor);
        }
        else
        {
            return;
        }


        /*
         * SmoothDamp avoids abrupt changes in intensity during
         * normal movement along either curve.
         */

        currentLightIntensity =
            Mathf.SmoothDamp(
                currentLightIntensity,
                targetIntensity,
                ref lightIntensityVelocity,
                lightIntensitySmoothTime);


        /*
         * Color follows smoothly as well.
         */

        float colorT =
            1f -
            Mathf.Exp(
                -lightColorSmoothTime *
                Time.deltaTime);


        currentLightColor =
            Color.Lerp(
                currentLightColor,
                targetColor,
                colorT);


        light.intensity =
            Mathf.Max(
                0f,
                currentLightIntensity);

        light.color =
            currentLightColor;


        /*
         * The active light NEVER gets disabled simply because
         * its intensity is small.
         *
         * The intersection system decides when ownership changes.
         */

        light.enabled =
            true;


        EnforceSingleActiveLight();
    }


    // ============================================================
    // CAPTURE CURRENT STATE
    // ============================================================

    private void CaptureCurrentLightState()
    {
        Light light =
            GetActiveLight();


        if (light == null)
            return;


        currentLightIntensity =
            light.intensity;

        currentLightColor =
            light.color;
    }


    // ============================================================
    // SUN
    // ============================================================

    private float CalculateSunIntensity(
        float dayFactor,
        float visibility)
    {
        float value =
            Mathf.Max(
                0f,
                sunIntensity.Evaluate(
                    dayFactor));


        /*
         * SunVisibility accounts for the astronomical visibility
         * of the Sun.
         */

        value *=
            Mathf.Clamp01(
                visibility);


        return value;
    }


    // ============================================================
    // MOON
    // ============================================================

    private float CalculateMoonIntensity(
        float moonAltitude)
    {
        float moonFactor =
            CalculateMoonFactor(
                moonAltitude);


        return Mathf.Max(
            0f,
            moonIntensity.Evaluate(
                moonFactor));
    }


    private float CalculateMoonFactor(
        float moonAltitude)
    {
        return Mathf.Clamp01(
            Mathf.InverseLerp(
                moonMinAltitude,
                moonMaxAltitude,
                moonAltitude));
    }


    // ============================================================
    // TWILIGHT
    // ============================================================

    private float CalculateTwilightFactor(
        float sunAltitude)
    {
        if (twilightStartAltitude <=
            twilightEndAltitude)
        {
            return 0f;
        }


        /*
         * -18° = 0
         * -6°  = 1
         */

        return Mathf.Clamp01(
            Mathf.InverseLerp(
                twilightEndAltitude,
                twilightStartAltitude,
                sunAltitude));
    }


    // ============================================================
    // AMBIENT
    // ============================================================

    private void UpdateAmbient(
        float sunDayFactor,
        float twilightFactor,
        float moonFactor)
    {
        /*
         * Ambient COLOR follows the Sun.
         *
         * Moon does not alter atmospheric color.
         */

        RenderSettings.ambientLight =
            ambientColor.Evaluate(
                sunDayFactor);


        /*
         * Solar ambient.
         */

        float baseIntensity =
            ambientIntensity.Evaluate(
                sunDayFactor);


        /*
         * Twilight provides additional indirect-looking
         * illumination around sunrise and sunset.
         */

        float twilightBrightness =
            twilightIntensity.Evaluate(
                twilightFactor);


        /*
         * Moon contributes brightness only.
         */

        float moonBrightness =
            moonFactor *
            moonAmbientContribution;


        /*
         * Never let the world become completely black.
         */

        float finalIntensity =
            Mathf.Max(
                baseIntensity,
                twilightBrightness * 0.18f,
                moonBrightness,
                minimumAmbientIntensity);


        RenderSettings.ambientIntensity =
            finalIntensity;
    }


    // ============================================================
    // FOG
    // ============================================================

    private void UpdateFog(
        float sunDayFactor)
    {
        /*
         * Fog is controlled ONLY by SunDayFactor.
         *
         * 0 = -6°
         * 1 = +6°
         *
         * Moon does not affect fog.
         */

        RenderSettings.fog =
            true;


        float factor =
            Mathf.Clamp01(
                sunDayFactor);


        RenderSettings.fogColor =
            fogColor.Evaluate(
                factor);


        RenderSettings.fogDensity =
            Mathf.Max(
                0f,
                fogDensity.Evaluate(
                    factor));
    }


    // ============================================================
    // POST PROCESSING
    // ============================================================

    private void UpdatePostProcessing(
        float sunDayFactor)
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value =
                postExposure.Evaluate(
                    sunDayFactor);


            colorAdjustments.saturation.value =
                saturation.Evaluate(
                    sunDayFactor);


            colorAdjustments.contrast.value =
                contrast.Evaluate(
                    sunDayFactor);


            colorAdjustments.colorFilter.value =
                colorFilter.Evaluate(
                    sunDayFactor);
        }


        if (bloom != null)
        {
            bloom.intensity.value =
                Mathf.Max(
                    0f,
                    bloomIntensity.Evaluate(
                        sunDayFactor));
        }


        if (vignette != null)
        {
            vignette.intensity.value =
                Mathf.Clamp01(
                    vignetteIntensity.Evaluate(
                        sunDayFactor));
        }
    }


    // ============================================================
    // LIGHT HELPERS
    // ============================================================

    private Light GetActiveLight()
    {
        switch (activeLight)
        {
            case ActiveLight.Sun:
                return sunLight;

            case ActiveLight.Moon:
                return moonLight;

            default:
                return null;
        }
    }


    private void EnforceSingleActiveLight()
    {
        if (sunLight != null)
        {
            sunLight.enabled =
                activeLight ==
                ActiveLight.Sun;
        }


        if (moonLight != null)
        {
            moonLight.enabled =
                activeLight ==
                ActiveLight.Moon;
        }
    }


    private void DisableAllLights()
    {
        if (sunLight != null)
            sunLight.enabled = false;


        if (moonLight != null)
            moonLight.enabled = false;
    }


    // ============================================================
    // VOLUME
    // ============================================================

    private void SetupVolume()
    {
        colorAdjustments = null;
        bloom = null;
        vignette = null;


        if (globalVolume == null)
            return;


        if (globalVolume.profile == null)
            return;


        globalVolume.profile.TryGet(
            out colorAdjustments);


        globalVolume.profile.TryGet(
            out bloom);


        globalVolume.profile.TryGet(
            out vignette);
    }


    // ============================================================
    // FORCE UPDATE
    // ============================================================

    private void ForceUpdate()
    {
        activeLight =
            ActiveLight.None;


        currentLightIntensity =
            0f;


        currentLightColor =
            Color.black;


        lightIntensityVelocity =
            0f;


        previousLightDifference =
            float.NaN;


        lastHandoffTime =
            -Mathf.Infinity;


        DisableAllLights();


        UpdateDayNight();
    }


    // ============================================================
    // PUBLIC API
    // ============================================================

    public float SunAltitude
    {
        get
        {
            if (astronomicalSystem == null)
                return -90f;


            return
                (float)
                astronomicalSystem.SunAltitude;
        }
    }


    public float MoonAltitude
    {
        get
        {
            if (astronomicalSystem == null)
                return -90f;


            return
                (float)
                astronomicalSystem.MoonAltitude;
        }
    }


    public float SunDayFactor
    {
        get
        {
            if (astronomicalSystem == null)
                return 0f;


            return
                astronomicalSystem.SunDayFactor;
        }
    }


    public float SunVisibility
    {
        get
        {
            if (astronomicalSystem == null)
                return 0f;


            return
                astronomicalSystem.SunVisibility;
        }
    }


    public float TwilightFactor
    {
        get
        {
            if (astronomicalSystem == null)
                return 0f;


            return CalculateTwilightFactor(
                (float)
                astronomicalSystem.SunAltitude);
        }
    }


    public float WorldLightFactor
    {
        get
        {
            /*
             * Kept for compatibility with any existing scripts
             * that may already reference this property.
             *
             * It no longer represents a third directional light.
             */

            if (astronomicalSystem == null)
                return minimumAmbientIntensity;


            float sunFactor =
                astronomicalSystem.SunDayFactor;


            float twilight =
                CalculateTwilightFactor(
                    (float)
                    astronomicalSystem.SunAltitude);


            float moonFactor =
                CalculateMoonFactor(
                    (float)
                    astronomicalSystem.MoonAltitude);


            float baseAmbient =
                ambientIntensity.Evaluate(
                    sunFactor);


            float twilightAmbient =
                twilightIntensity.Evaluate(
                    twilight) *
                0.18f;


            float moonAmbient =
                moonFactor *
                moonAmbientContribution;


            return Mathf.Clamp01(
                Mathf.Max(
                    baseAmbient,
                    twilightAmbient,
                    moonAmbient,
                    minimumAmbientIntensity));
        }
    }


    public bool IsDay()
    {
        if (astronomicalSystem == null)
            return false;


        return astronomicalSystem.IsDay;
    }


    public bool IsNight()
    {
        if (astronomicalSystem == null)
            return true;


        return astronomicalSystem.IsNight;
    }
}
