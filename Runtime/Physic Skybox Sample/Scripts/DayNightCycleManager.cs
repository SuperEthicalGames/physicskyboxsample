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
     * 0 = Sol por debajo de -6°
     * 1 = Sol a +6° o superior
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
     * X = Moon altitude normalizada.
     *
     * -6°  = 0
     * +6°  = 1
     */
    [SerializeField]
    private AnimationCurve moonIntensity =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);


    // ============================================================
    // AMBIENT
    // ============================================================

    [Header("Ambient Light")]

    [SerializeField]
    private Gradient ambientColor;

    /*
     * X = SunDayFactor
     */
    [SerializeField]
    private AnimationCurve ambientIntensity =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);


    // ============================================================
    // FOG
    // ============================================================

    [Header("Fog")]

    [SerializeField]
    private Gradient fogColor;

    /*
     * X = SunDayFactor
     */
    [SerializeField]
    private AnimationCurve fogDensity =
        AnimationCurve.Linear(
            0f,
            0.02f,
            1f,
            0.01f
        );


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

    /*
     * X = SunDayFactor
     */
    [SerializeField]
    private AnimationCurve postExposure =
        AnimationCurve.Linear(
            0f,
            -1f,
            1f,
            0f
        );

    [SerializeField]
    private Gradient colorFilter;

    [SerializeField]
    private AnimationCurve saturation =
        AnimationCurve.Linear(
            0f,
            -20f,
            1f,
            0f
        );

    [SerializeField]
    private AnimationCurve contrast =
        AnimationCurve.Linear(
            0f,
            10f,
            1f,
            0f
        );


    // ============================================================
    // BLOOM
    // ============================================================

    [Header("Bloom")]

    [SerializeField]
    private AnimationCurve bloomIntensity =
        AnimationCurve.Linear(
            0f,
            0.2f,
            1f,
            0.5f
        );


    // ============================================================
    // VIGNETTE
    // ============================================================

    [Header("Vignette")]

    [SerializeField]
    private AnimationCurve vignetteIntensity =
        AnimationCurve.Linear(
            0f,
            0.3f,
            1f,
            0.1f
        );


    // ============================================================
    // MOON SETTINGS
    // ============================================================

    [Header("Moon Curve Range")]

    [Tooltip("Moon altitude mapped to curve 0.")]
    [SerializeField]
    private float moonMinAltitude = -6f;

    [Tooltip("Moon altitude mapped to curve 1.")]
    [SerializeField]
    private float moonMaxAltitude = 6f;


    // ============================================================
    // URP REFERENCES
    // ============================================================

    private ColorAdjustments colorAdjustments;
    private Bloom bloom;
    private Vignette vignette;


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        SetupVolume();
    }


    private void OnEnable()
    {
        SetupVolume();
        UpdateDayNight();
    }


    private void Start()
    {
        SetupVolume();
        UpdateDayNight();
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

        UpdateSun();
        UpdateMoon();
        UpdateAmbient();
        UpdateFog();
        UpdatePostProcessing();
    }


    // ============================================================
    // SUN
    // ============================================================

    private void UpdateSun()
    {
        if (sunLight == null)
            return;


        /*
         * IMPORTANT:
         *
         * AstronomicalTimeSystem owns the Sun rotation.
         *
         * We DO NOT rotate the Sun here.
         */


        float dayFactor =
            astronomicalSystem.SunDayFactor;


        /*
         * Color uses the same astronomical domain.
         */

        sunLight.color =
            sunColor.Evaluate(
                dayFactor
            );


        /*
         * Intensity is controlled by SunDayFactor.
         *
         * -6°  -> curve X = 0
         * +6°  -> curve X = 1
         */

        sunLight.intensity =
            Mathf.Max(
                0f,
                sunIntensity.Evaluate(
                    dayFactor
                )
            );


        /*
         * SunVisibility is stricter than SunDayFactor.
         *
         * It considers the apparent solar disk
         * and atmospheric refraction.
         */

        sunLight.enabled =
            astronomicalSystem.SunVisibility > 0.001f;
    }


    // ============================================================
    // MOON
    // ============================================================

    private void UpdateMoon()
    {
        if (moonLight == null)
            return;


        float moonAltitude =
            (float)
            astronomicalSystem.MoonAltitude;


        /*
         * Normalize Moon altitude.
         *
         * -6° = 0
         * +6° = 1
         */

        float moonFactor =
            Mathf.Clamp01(
                Mathf.InverseLerp(
                    moonMinAltitude,
                    moonMaxAltitude,
                    moonAltitude
                )
            );


        moonLight.color =
            moonColor.Evaluate(
                moonFactor
            );


        moonLight.intensity =
            Mathf.Max(
                0f,
                moonIntensity.Evaluate(
                    moonFactor
                )
            );


        /*
         * Moon is physically below the horizon.
         */

        moonLight.enabled =
            moonAltitude > moonMinAltitude;
    }


    // ============================================================
    // AMBIENT
    // ============================================================

    private void UpdateAmbient()
    {
        float dayFactor =
            astronomicalSystem.SunDayFactor;


        RenderSettings.ambientLight =
            ambientColor.Evaluate(
                dayFactor
            );


        RenderSettings.ambientIntensity =
            Mathf.Max(
                0f,
                ambientIntensity.Evaluate(
                    dayFactor
                )
            );
    }


    // ============================================================
    // FOG
    // ============================================================

    private void UpdateFog()
    {
        float dayFactor =
            astronomicalSystem.SunDayFactor;


        RenderSettings.fog = true;


        RenderSettings.fogColor =
            fogColor.Evaluate(
                dayFactor
            );


        RenderSettings.fogDensity =
            Mathf.Max(
                0f,
                fogDensity.Evaluate(
                    dayFactor
                )
            );
    }


    // ============================================================
    // POST PROCESSING
    // ============================================================

    private void UpdatePostProcessing()
    {
        float dayFactor =
            astronomicalSystem.SunDayFactor;


        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value =
                postExposure.Evaluate(
                    dayFactor
                );


            colorAdjustments.saturation.value =
                saturation.Evaluate(
                    dayFactor
                );


            colorAdjustments.contrast.value =
                contrast.Evaluate(
                    dayFactor
                );


            colorAdjustments.colorFilter.value =
                colorFilter.Evaluate(
                    dayFactor
                );
        }


        if (bloom != null)
        {
            bloom.intensity.value =
                Mathf.Max(
                    0f,
                    bloomIntensity.Evaluate(
                        dayFactor
                    )
                );
        }


        if (vignette != null)
        {
            vignette.intensity.value =
                Mathf.Clamp01(
                    vignetteIntensity.Evaluate(
                        dayFactor
                    )
                );
        }
    }


    // ============================================================
    // URP VOLUME
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
            out colorAdjustments
        );


        globalVolume.profile.TryGet(
            out bloom
        );


        globalVolume.profile.TryGet(
            out vignette
        );
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

            return (float)
                astronomicalSystem.SunAltitude;
        }
    }


    public float MoonAltitude
    {
        get
        {
            if (astronomicalSystem == null)
                return -90f;

            return (float)
                astronomicalSystem.MoonAltitude;
        }
    }


    public float SunDayFactor
    {
        get
        {
            if (astronomicalSystem == null)
                return 0f;

            return astronomicalSystem.SunDayFactor;
        }
    }


    public float SunVisibility
    {
        get
        {
            if (astronomicalSystem == null)
                return 0f;

            return astronomicalSystem.SunVisibility;
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