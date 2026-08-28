using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sistema astronómico para Unity.
///
/// Responsabilidades:
/// - Mantener el tiempo UTC.
/// - Convertir tiempo manual local/UTC.
/// - Simular el paso del tiempo.
/// - Calcular Sol y Luna.
/// - Calcular tiempo sidéreo local.
/// - Calcular posición horizontal geométrica y aparente.
/// - Calcular tiempo solar aparente.
/// - Exponer fases/factores astronómicos para sistemas visuales.
/// - Orientar los Transform del Sol y la Luna.
/// - Actualizar parámetros globales para shaders.
///
/// Convención espacial:
/// Norte = +Z
/// Este  = +X
/// Arriba = +Y
///
/// Azimut:
/// 0   = Norte
/// 90  = Este
/// 180 = Sur
/// 270 = Oeste
///
/// IMPORTANTE:
/// Este componente es la FUENTE DE VERDAD astronómica.
/// Los sistemas visuales no deben crear su propio reloj.
/// </summary>
[ExecuteAlways]
public sealed class AstronomicalTimeSystem : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("ASTRONOMICAL OBJECTS")]
    [SerializeField] private Transform _Sun;
    [SerializeField] private Transform _Moon;

    // ============================================================
    // OBSERVER
    // ============================================================

    [Header("OBSERVER")]
    [Tooltip("Latitude. North positive, South negative.")]
    [Range(-90.0f, 90.0f)]
    [SerializeField] private double _Latitude = 6.2442;

    [Tooltip("Longitude. East positive, West negative.")]
    [Range(-180.0f, 180.0f)]
    [SerializeField] private double _Longitude = -75.5812;

    // ============================================================
    // TIME
    // ============================================================

    public enum TimeMode
    {
        RealTime,
        ManualLocal,
        ManualUTC,
        Simulated
    }

    [Header("TIME")]
    [SerializeField] private TimeMode _TimeMode = TimeMode.RealTime;

    [Tooltip("Astronomical seconds that pass for every real second.")]
    [Min(0.0f)]
    [SerializeField] private double _TimeScale = 60.0;

    // ============================================================
    // MANUAL DATE
    // ============================================================

    [Header("MANUAL DATE")]
    [SerializeField] private int _Year = 2026;

    [Range(1, 12)]
    [SerializeField] private int _Month = 8;

    [Range(1, 31)]
    [SerializeField] private int _Day = 21;

    [Range(0, 23)]
    [SerializeField] private int _Hour = 6;

    [Range(0, 59)]
    [SerializeField] private int _Minute = 0;

    [Range(0, 59)]
    [SerializeField] private int _Second = 0;

    [Range(0, 999)]
    [SerializeField] private int _Millisecond = 0;

    [Tooltip(
        "UTC offset used by ManualLocal and ManualUTC.\n" +
        "Example: Colombia = -5, Japan = +9."
    )]
    [Range(-14, 14)]
    [SerializeField] private double _ManualUTCOffsetHours = -5.0;

    // ============================================================
    // ASTRONOMY
    // ============================================================

    [Header("ASTRONOMY")]
    [Tooltip("Apply atmospheric refraction to apparent Sun/Moon altitude.")]
    [SerializeField] private bool _UseAtmosphericRefraction = true;

    [Tooltip("Apply topocentric correction to the Moon.")]
    [SerializeField] private bool _UseLunarTopocentricCorrection = true;

    // ============================================================
    // ROTATION
    // ============================================================

    [Header("ROTATION")]
    [SerializeField] private Vector3 _SunRotationOffset;
    [SerializeField] private Vector3 _MoonRotationOffset;

    // ============================================================
    // SKYBOX
    // ============================================================

    [Header("SKYBOX")]
    [SerializeField] private bool _UpdateShaderGlobals = true;

    // ============================================================
    // DEBUG
    // ============================================================

    [Header("DEBUG")]
    [SerializeField] private string _CurrentUTCTime;
    [SerializeField] private string _CurrentLocalTime;
    [SerializeField] private string _DeviceTimeZone;
    [SerializeField] private double _DeviceUTCOffsetHours;

    [SerializeField] private double _JulianDate;
    [SerializeField] private double _LocalSiderealTime;

    [SerializeField] private double _SunRightAscension;
    [SerializeField] private double _SunDeclination;

    [SerializeField] private double _SunAltitude;
    [SerializeField] private double _SunAzimuth;

    [SerializeField] private double _MoonAltitude;
    [SerializeField] private double _MoonAzimuth;

    [SerializeField] private double _MoonDistanceEarthRadii;

    [SerializeField] private double _SolarHourAngle;
    [SerializeField] private double _SolarTimeHours;
    [SerializeField] private double _SolarCycle;

    [SerializeField] private double _MoonPhaseAngle;
    [SerializeField] private double _MoonIllumination;

    // ============================================================
    // INTERNAL STATE
    // ============================================================

    private DateTime _CurrentUTC;

    private bool _initialized;

    private TimeMode _previousTimeMode;

    private double _previousLatitude;
    private double _previousLongitude;

    private int _previousYear;
    private int _previousMonth;
    private int _previousDay;
    private int _previousHour;
    private int _previousMinute;
    private int _previousSecond;
    private int _previousMillisecond;

    private double _previousManualUTCOffset;

    // ============================================================
    // SHADER IDS
    // ============================================================

    private static readonly int SunDirID = Shader.PropertyToID("_Sun_Direction");
    private static readonly int MoonDirID = Shader.PropertyToID("_Moon_Direction");
    private static readonly int MoonSpaceMatrixID = Shader.PropertyToID("_Moon_Space_Matrix");
    private static readonly int StarLatitudeID = Shader.PropertyToID("_Star_Latitude");
    private static readonly int StarSiderealTimeID = Shader.PropertyToID("_Star_Sidereal_Time");

    // ============================================================
    // PUBLIC TIME API
    // ============================================================

    public DateTime CurrentUTC => _CurrentUTC;

    public TimeMode CurrentTimeMode => _TimeMode;

    public double TimeScale => _TimeScale;

    // ============================================================
    // PUBLIC OBSERVER API
    // ============================================================

    public double Latitude => _Latitude;

    public double Longitude => _Longitude;

    public double ManualUTCOffsetHours => _ManualUTCOffsetHours;

    // ============================================================
    // PUBLIC ASTRONOMICAL API
    // ============================================================

    /// <summary>
    /// Julian Date currently being evaluated.
    /// </summary>
    public double JulianDate => _JulianDate;

    /// <summary>
    /// Local Sidereal Time in degrees [0,360).
    /// </summary>
    public double LocalSiderealTime => _LocalSiderealTime;

    // ============================================================
    // SUN
    // ============================================================

    /// <summary>
    /// Sun altitude after optional atmospheric refraction.
    /// Degrees.
    /// </summary>
    public double SunAltitude => _SunAltitude;

    /// <summary>
    /// Sun azimuth.
    /// 0 = North, 90 = East, 180 = South, 270 = West.
    /// </summary>
    public double SunAzimuth => _SunAzimuth;

    /// <summary>
    /// Geometric Sun altitude before atmospheric refraction.
    /// </summary>
    public double SunGeometricAltitude => _SunGeometricAltitude;

    /// <summary>
    /// Sun right ascension in degrees.
    /// </summary>
    public double SunRightAscension => _SunRightAscension;

    /// <summary>
    /// Sun declination in degrees.
    /// </summary>
    public double SunDeclination => _SunDeclination;

    // ============================================================
    // MOON
    // ============================================================

    /// <summary>
    /// Moon altitude after optional atmospheric refraction.
    /// </summary>
    public double MoonAltitude => _MoonAltitude;

    /// <summary>
    /// Moon azimuth.
    /// </summary>
    public double MoonAzimuth => _MoonAzimuth;

    /// <summary>
    /// Geometric Moon altitude before atmospheric refraction.
    /// </summary>
    public double MoonGeometricAltitude => _MoonGeometricAltitude;

    /// <summary>
    /// Moon distance in Earth radii.
    /// </summary>
    public double MoonDistanceEarthRadii => _MoonDistanceEarthRadii;

    /// <summary>
    /// Approximate geocentric phase angle of the Moon.
    ///
    /// 0°   = New Moon
    /// 90°  = First/Last Quarter
    /// 180° = Full Moon
    /// </summary>
    public double MoonPhaseAngle => _MoonPhaseAngle;

    /// <summary>
    /// Approximate illuminated fraction of the Moon.
    ///
    /// 0 = New Moon
    /// 1 = Full Moon
    /// </summary>
    public float MoonIllumination => (float)_MoonIllumination;

    // ============================================================
    // SOLAR TIME
    // ============================================================

    /// <summary>
    /// Apparent solar hour angle in degrees.
    ///
    /// -180° = solar midnight
    ///  -90° = approximately 06:00 solar time
    ///    0° = solar noon
    ///  +90° = approximately 18:00 solar time
    /// +180° = solar midnight
    /// </summary>
    public double SolarHourAngle => _SolarHourAngle;

    /// <summary>
    /// Apparent solar time in hours [0,24).
    ///
    /// This is NOT UTC.
    /// This is NOT the computer's local timezone.
    ///
    /// 00 = apparent solar midnight
    /// 06 = approximately solar morning
    /// 12 = apparent solar noon
    /// 18 = approximately solar evening
    /// </summary>
    public double SolarTimeHours => _SolarTimeHours;

    /// <summary>
    /// Continuous cyclic solar coordinate [0,1).
    ///
    /// 0.00 = solar midnight
    /// 0.25 = ~06:00 solar time
    /// 0.50 = solar noon
    /// 0.75 = ~18:00 solar time
    ///
    /// This is the principal time coordinate that the
    /// DayNightCycleManager can use for cyclic curves.
    /// </summary>
    public float SolarCycle => (float)_SolarCycle;

    // ============================================================
    // SOLAR FACTORS
    // ============================================================

    /// <summary>
    /// 0..1 transition through civil twilight/day.
    ///
    /// -6° = 0
    /// +6° = 1
    /// </summary>
    public float SunDayFactor
    {
        get { return Mathf.Clamp01(Mathf.InverseLerp(-6f, 6f, (float)_SunAltitude)); }
    }

    /// <summary>
    /// 0..1 apparent visibility of the Sun near the horizon.
    ///
    /// Approximately:
    /// -0.833° = 0
    /// +1°     = 1
    /// </summary>
    public float SunVisibility
    {
        get { return Mathf.Clamp01(Mathf.InverseLerp(-0.833f, 1f, (float)_SunAltitude)); }
    }

    /// <summary>
    /// 0..1 daylight factor based on solar altitude.
    /// </summary>
    public float DayFactor
    {
        get { return Mathf.Clamp01(Mathf.InverseLerp(-6f, 6f, (float)_SunAltitude)); }
    }

    /// <summary>
    /// 0..1 night factor.
    ///
    /// 0°  = beginning of night
    /// -18° = full astronomical night
    /// </summary>
    public float NightFactor
    {
        get
        {
            float altitude = (float)_SunAltitude;
            return Mathf.Clamp01(Mathf.InverseLerp(0f, -18f, altitude));
        }
    }

    /// <summary>
    /// 0..1 twilight factor.
    ///
    /// Maximum around the horizon.
    /// Falls toward both day and deep night.
    /// </summary>
    public float TwilightFactor
    {
        get
        {
            float altitude = Mathf.Abs((float)_SunAltitude);
            return 1f - Mathf.Clamp01(altitude / 6f);
        }
    }

    public bool IsDay => _SunAltitude > 0.0;

    public bool IsNight => _SunAltitude <= 0.0;

    // ============================================================
    // UNITY
    // ============================================================

    private void OnEnable()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (!_initialized)
            Initialize();

        UpdateTime();
        CalculateAstronomy();
        UpdateDebugInformation();
        UpdateShaderGlobals();
    }

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void Initialize()
    {
        _previousTimeMode = _TimeMode;
        _CurrentUTC = CreateInitialUTC();

        CacheState();

        _initialized = true;

        CalculateAstronomy();
        UpdateDebugInformation();
        UpdateShaderGlobals();
    }

    // ============================================================
    // INITIAL UTC
    // ============================================================

    private DateTime CreateInitialUTC()
    {
        switch (_TimeMode)
        {
            case TimeMode.RealTime:
                return DateTime.UtcNow;

            case TimeMode.ManualLocal:
                return CreateManualLocalUTC();

            case TimeMode.ManualUTC:
                return CreateManualUTC();

            case TimeMode.Simulated:
                return CreateManualUTC();

            default:
                return DateTime.UtcNow;
        }
    }

    // ============================================================
    // TIME
    // ============================================================

    private void UpdateTime()
    {
        if (_TimeMode != _previousTimeMode)
        {
            HandleTimeModeChanged();
            _previousTimeMode = _TimeMode;
        }

        switch (_TimeMode)
        {
            case TimeMode.RealTime:
                _CurrentUTC = DateTime.UtcNow;
                break;

            case TimeMode.ManualLocal:
                if (ManualSettingsChanged())
                {
                    _CurrentUTC = CreateManualLocalUTC();
                    CacheState();
                }
                break;

            case TimeMode.ManualUTC:
                if (ManualSettingsChanged())
                {
                    _CurrentUTC = CreateManualUTC();
                    CacheState();
                }
                break;

            case TimeMode.Simulated:
                UpdateSimulatedTime();
                break;
        }
    }

    // ============================================================
    // MODE CHANGE
    // ============================================================

    private void HandleTimeModeChanged()
    {
        switch (_TimeMode)
        {
            case TimeMode.RealTime:
                _CurrentUTC = DateTime.UtcNow;
                break;

            case TimeMode.ManualLocal:
                _CurrentUTC = CreateManualLocalUTC();
                break;

            case TimeMode.ManualUTC:
                _CurrentUTC = CreateManualUTC();
                break;

            case TimeMode.Simulated:
                // Keep current simulation time.
                break;
        }

        CacheState();
    }

    // ============================================================
    // SIMULATION
    // ============================================================

    private void UpdateSimulatedTime()
    {
        if (_TimeScale <= 0.0)
            return;

        double realDeltaSeconds = Math.Max(0.0, Time.deltaTime);

        if (realDeltaSeconds <= 0.0)
            return;

        _CurrentUTC = _CurrentUTC.AddSeconds(realDeltaSeconds * _TimeScale);
    }

    // ============================================================
    // MANUAL LOCAL
    // ============================================================

    private DateTime CreateManualLocalUTC()
    {
        /*
         * IMPORTANT:
         *
         * Do NOT use TimeZoneInfo.Local here.
         *
         * The game world can be in any location and Unity may
         * be running on a computer in a completely different
         * timezone.
         *
         * ManualLocal therefore means:
         *
         * "The supplied date/time is local to the configured
         * observer offset."
         */

        DateTime local = CreateManualDateTime(DateTimeKind.Unspecified);
        TimeSpan offset = TimeSpan.FromHours(_ManualUTCOffsetHours);
        DateTime utc = local - offset;

        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    // ============================================================
    // MANUAL UTC
    // ============================================================

    private DateTime CreateManualUTC()
    {
        DateTime utc = CreateManualDateTime(DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    // ============================================================
    // MANUAL DATETIME
    // ============================================================

    private DateTime CreateManualDateTime(DateTimeKind kind)
    {
        int year = Mathf.Clamp(_Year, 1, 9999);
        int month = Mathf.Clamp(_Month, 1, 12);
        int maxDay = DateTime.DaysInMonth(year, month);
        int day = Mathf.Clamp(_Day, 1, maxDay);
        int hour = Mathf.Clamp(_Hour, 0, 23);
        int minute = Mathf.Clamp(_Minute, 0, 59);
        int second = Mathf.Clamp(_Second, 0, 59);
        int millisecond = Mathf.Clamp(_Millisecond, 0, 999);

        return new DateTime(year, month, day, hour, minute, second, millisecond, kind);
    }

    // ============================================================
    // CHANGE DETECTION
    // ============================================================

    private bool ManualSettingsChanged()
    {
        return
            _Year != _previousYear ||
            _Month != _previousMonth ||
            _Day != _previousDay ||
            _Hour != _previousHour ||
            _Minute != _previousMinute ||
            _Second != _previousSecond ||
            _Millisecond != _previousMillisecond ||
            Math.Abs(_Latitude - _previousLatitude) > 1e-9 ||
            Math.Abs(_Longitude - _previousLongitude) > 1e-9 ||
            Math.Abs(_ManualUTCOffsetHours - _previousManualUTCOffset) > 1e-9;
    }

    // ============================================================
    // CACHE
    // ============================================================

    private void CacheState()
    {
        _previousYear = _Year;
        _previousMonth = _Month;
        _previousDay = _Day;
        _previousHour = _Hour;
        _previousMinute = _Minute;
        _previousSecond = _Second;
        _previousMillisecond = _Millisecond;
        _previousLatitude = _Latitude;
        _previousLongitude = _Longitude;
        _previousManualUTCOffset = _ManualUTCOffsetHours;
    }

    // ============================================================
    // ASTRONOMY
    // ============================================================

    private void CalculateAstronomy()
    {
        _JulianDate = CalculateJulianDate(_CurrentUTC);
        _LocalSiderealTime = CalculateLocalSiderealTime(_JulianDate, _Longitude);

        // --------------------------------------------------------
        // SUN
        // --------------------------------------------------------

        EquatorialCoordinates sun = CalculateSunEquatorial(_JulianDate);

        _SunRightAscension = sun.RightAscension;
        _SunDeclination = sun.Declination;

        HorizontalCoordinates sunHorizontal = EquatorialToHorizontal(
            sun.RightAscension,
            sun.Declination,
            _Latitude,
            _LocalSiderealTime
        );

        _SunGeometricAltitude = sunHorizontal.Altitude;

        if (_UseAtmosphericRefraction)
        {
            sunHorizontal.Altitude = ApplyRefraction(sunHorizontal.Altitude);
        }

        _SunAltitude = sunHorizontal.Altitude;
        _SunAzimuth = sunHorizontal.Azimuth;

        // --------------------------------------------------------
        // SOLAR TIME
        // --------------------------------------------------------

        CalculateSolarTime(sun.RightAscension);

        // --------------------------------------------------------
        // MOON
        // --------------------------------------------------------

        EquatorialCoordinates moon = CalculateMoonEquatorial(_JulianDate);

        _MoonDistanceEarthRadii = moon.DistanceEarthRadii;

        HorizontalCoordinates moonHorizontal;

        if (_UseLunarTopocentricCorrection)
        {
            moonHorizontal = EquatorialToTopocentricHorizontal(
                moon,
                _Latitude,
                _LocalSiderealTime
            );
        }
        else
        {
            moonHorizontal = EquatorialToHorizontal(
                moon.RightAscension,
                moon.Declination,
                _Latitude,
                _LocalSiderealTime
            );
        }

        _MoonGeometricAltitude = moonHorizontal.Altitude;

        if (_UseAtmosphericRefraction)
        {
            moonHorizontal.Altitude = ApplyRefraction(moonHorizontal.Altitude);
        }

        _MoonAltitude = moonHorizontal.Altitude;
        _MoonAzimuth = moonHorizontal.Azimuth;

        // --------------------------------------------------------
        // MOON PHASE
        // --------------------------------------------------------

        CalculateMoonPhase(moon);

        // --------------------------------------------------------
        // UNITY DIRECTIONS
        // --------------------------------------------------------

        Vector3 sunDirection = HorizontalToUnity(_SunAltitude, _SunAzimuth);
        Vector3 moonDirection = HorizontalToUnity(_MoonAltitude, _MoonAzimuth);

        // --------------------------------------------------------
        // TRANSFORMS
        // --------------------------------------------------------

        if (_Sun != null)
        {
            SetCelestialRotation(_Sun, sunDirection, _SunRotationOffset);
        }

        if (_Moon != null)
        {
            SetCelestialRotation(_Moon, moonDirection, _MoonRotationOffset);
        }

        // --------------------------------------------------------
        // SHADERS
        // --------------------------------------------------------

        if (_UpdateShaderGlobals)
        {
            SendShaderGlobals(sunDirection, moonDirection);
        }
    }

    // ============================================================
    // SOLAR TIME
    // ============================================================

    private void CalculateSolarTime(double sunRightAscension)
    {
        /*
         * Hour angle:
         *
         * H = LST - RA
         *
         * H = 0° at apparent solar noon.
         *
         * 15° = one solar hour.
         */

        double hourAngle = NormalizeSignedDegrees(_LocalSiderealTime - sunRightAscension);

        _SolarHourAngle = hourAngle;

        /*
         * Convert hour angle to apparent solar time.
         *
         * H = -180° → 00:00
         * H =  -90° → 06:00
         * H =    0° → 12:00
         * H =  +90° → 18:00
         * H = +180° → 24:00
         */

        double solarTime = 12.0 + hourAngle / 15.0;

        _SolarTimeHours = NormalizeHours(solarTime);

        /*
         * 24 hours → 0..1.
         *
         * This is cyclic by construction.
         */

        _SolarCycle = _SolarTimeHours / 24.0;
    }

    // ============================================================
    // MOON PHASE
    // ============================================================

    private void CalculateMoonPhase(EquatorialCoordinates moon)
    {
        /*
         * The angle between the geocentric directions
         * Sun -> observer and Moon -> observer.
         *
         * 0°   = New Moon
         * 180° = Full Moon
         */

        double sun = DegreesToRadians(_SunRightAscension);
        double sunDec = DegreesToRadians(_SunDeclination);
        double moonRA = DegreesToRadians(moon.RightAscension);
        double moonDec = DegreesToRadians(moon.Declination);

        double sunX = Math.Cos(sunDec) * Math.Cos(sun);
        double sunY = Math.Cos(sunDec) * Math.Sin(sun);
        double sunZ = Math.Sin(sunDec);

        double moonX = Math.Cos(moonDec) * Math.Cos(moonRA);
        double moonY = Math.Cos(moonDec) * Math.Sin(moonRA);
        double moonZ = Math.Sin(moonDec);

        double dot = sunX * moonX + sunY * moonY + sunZ * moonZ;

        dot = Clamp(dot, -1.0, 1.0);

        double phaseAngle = RadiansToDegrees(Math.Acos(dot));

        _MoonPhaseAngle = phaseAngle;

        /*
         * Illuminated fraction:
         *
         * k = (1 - cos(phase)) / 2
         */

        _MoonIllumination = 0.5 * (1.0 - Math.Cos(DegreesToRadians(phaseAngle)));
    }

    // ============================================================
    // SUN
    // ============================================================

    private EquatorialCoordinates CalculateSunEquatorial(double jd)
    {
        double T = (jd - 2451545.0) / 36525.0;

        double L0 = NormalizeDegrees(280.46646 + 36000.76983 * T + 0.0003032 * T * T);

        double M = NormalizeDegrees(357.52911 + 35999.05029 * T - 0.0001537 * T * T);

        double Mrad = DegreesToRadians(M);

        double C =
            (1.914602 - 0.004817 * T - 0.000014 * T * T) * Math.Sin(Mrad)
            + (0.019993 - 0.000101 * T) * Math.Sin(2.0 * Mrad)
            + 0.000289 * Math.Sin(3.0 * Mrad);

        double trueLongitude = L0 + C;

        double omega = 125.04 - 1934.136 * T;

        double apparentLongitude =
            trueLongitude - 0.00569 - 0.00478 * Math.Sin(DegreesToRadians(omega));

        double epsilon0 =
            23.0 +
            (26.0 +
                (21.448 - 46.8150 * T - 0.00059 * T * T + 0.001813 * T * T * T) / 60.0
            ) / 60.0;

        double epsilon = epsilon0 + 0.00256 * Math.Cos(DegreesToRadians(omega));

        double lambdaRad = DegreesToRadians(apparentLongitude);
        double epsilonRad = DegreesToRadians(epsilon);

        double ra = Math.Atan2(
            Math.Cos(epsilonRad) * Math.Sin(lambdaRad),
            Math.Cos(lambdaRad)
        );

        double dec = Math.Asin(
            Clamp(Math.Sin(epsilonRad) * Math.Sin(lambdaRad), -1.0, 1.0)
        );

        return new EquatorialCoordinates
        {
            RightAscension = NormalizeDegrees(RadiansToDegrees(ra)),
            Declination = RadiansToDegrees(dec),
            DistanceEarthRadii = 0.0
        };
    }

    // ============================================================
    // MOON
    // ============================================================

    private EquatorialCoordinates CalculateMoonEquatorial(double jd)
    {
        double d = jd - 2451543.5;

        double N = NormalizeDegrees(125.1228 - 0.0529538083 * d);
        double i = 5.1454;
        double w = NormalizeDegrees(318.0634 + 0.1643573223 * d);
        double a = 60.2666;
        double e = 0.054900;
        double M = NormalizeDegrees(115.3654 + 13.0649929509 * d);

        double E = SolveKepler(DegreesToRadians(M), e);

        double xv = a * (Math.Cos(E) - e);
        double yv = a * Math.Sqrt(1.0 - e * e) * Math.Sin(E);

        double v = Math.Atan2(yv, xv);
        double r = Math.Sqrt(xv * xv + yv * yv);

        double NRad = DegreesToRadians(N);
        double iRad = DegreesToRadians(i);
        double wRad = DegreesToRadians(w);

        double vw = v + wRad;

        double xh =
            r * (Math.Cos(NRad) * Math.Cos(vw) - Math.Sin(NRad) * Math.Sin(vw) * Math.Cos(iRad));

        double yh =
            r * (Math.Sin(NRad) * Math.Cos(vw) + Math.Cos(NRad) * Math.Sin(vw) * Math.Cos(iRad));

        double zh = r * Math.Sin(vw) * Math.Sin(iRad);

        // --------------------------------------------------------
        // Sun approximation used for lunar perturbations.
        // --------------------------------------------------------

        double sunM = NormalizeDegrees(356.0470 + 0.9856002585 * d);
        double sunw = NormalizeDegrees(282.9404 + 4.70935E-5 * d);
        double sunE = SolveKepler(DegreesToRadians(sunM), 0.016709 - 1.151E-9 * d);
        double sunEccentricity = 0.016709 - 1.151E-9 * d;

        double sunX = Math.Cos(sunE) - sunEccentricity;
        double sunY = Math.Sqrt(1.0 - sunEccentricity * sunEccentricity) * Math.Sin(sunE);

        double sunTrueAnomaly = Math.Atan2(sunY, sunX);

        double sunLongitude = NormalizeDegrees(RadiansToDegrees(sunTrueAnomaly) + sunw);

        double moonMeanLongitude = NormalizeDegrees(N + w + M);

        double elongation = NormalizeDegrees(moonMeanLongitude - sunLongitude);

        // --------------------------------------------------------
        // Longitude perturbations.
        // --------------------------------------------------------

        double longitudeCorrection =
            -1.274 * Math.Sin(DegreesToRadians(M - 2.0 * elongation))
            + 0.658 * Math.Sin(DegreesToRadians(2.0 * elongation))
            - 0.186 * Math.Sin(DegreesToRadians(sunM))
            - 0.059 * Math.Sin(DegreesToRadians(2.0 * M - 2.0 * elongation))
            - 0.057 * Math.Sin(DegreesToRadians(M - 2.0 * elongation + sunM))
            + 0.053 * Math.Sin(DegreesToRadians(M + 2.0 * elongation));

        // --------------------------------------------------------
        // Latitude perturbations.
        // --------------------------------------------------------

        double latitudeCorrection =
            -0.173 * Math.Sin(DegreesToRadians(N - 2.0 * elongation))
            - 0.055 * Math.Sin(DegreesToRadians(M - N - 2.0 * elongation))
            - 0.046 * Math.Sin(DegreesToRadians(M + N - 2.0 * elongation))
            + 0.033 * Math.Sin(DegreesToRadians(N + 2.0 * elongation));

        double longitude = RadiansToDegrees(Math.Atan2(yh, xh));
        double latitude = RadiansToDegrees(Math.Atan2(zh, Math.Sqrt(xh * xh + yh * yh)));

        longitude += longitudeCorrection;
        latitude += latitudeCorrection;

        // --------------------------------------------------------
        // Ecliptic -> Equatorial.
        // --------------------------------------------------------

        double lonRad = DegreesToRadians(longitude);
        double latRad = DegreesToRadians(latitude);

        xh = r * Math.Cos(latRad) * Math.Cos(lonRad);
        yh = r * Math.Cos(latRad) * Math.Sin(lonRad);
        zh = r * Math.Sin(latRad);

        double epsilon = DegreesToRadians(23.4393);

        double xe = xh;
        double ye = yh * Math.Cos(epsilon) - zh * Math.Sin(epsilon);
        double ze = yh * Math.Sin(epsilon) + zh * Math.Cos(epsilon);

        double ra = Math.Atan2(ye, xe);
        double dec = Math.Atan2(ze, Math.Sqrt(xe * xe + ye * ye));

        return new EquatorialCoordinates
        {
            RightAscension = NormalizeDegrees(RadiansToDegrees(ra)),
            Declination = RadiansToDegrees(dec),
            DistanceEarthRadii = r
        };
    }

    // ============================================================
    // TOPOCENTRIC MOON
    // ============================================================

    private HorizontalCoordinates EquatorialToTopocentricHorizontal(
        EquatorialCoordinates body,
        double latitude,
        double localSiderealTime
    )
    {
        double distance = Math.Max(body.DistanceEarthRadii, 1.0);

        double phi = DegreesToRadians(latitude);
        double dec = DegreesToRadians(body.Declination);
        double H = DegreesToRadians(
            NormalizeSignedDegrees(localSiderealTime - body.RightAscension)
        );

        const double f = 1.0 / 298.257223563;

        double u = Math.Atan((1.0 - f) * Math.Tan(phi));

        double rhoSinPhi = (1.0 - f) * Math.Sin(u);
        double rhoCosPhi = Math.Cos(u);

        double sinPi = 1.0 / distance;

        double deltaAlpha = Math.Atan2(
            -rhoCosPhi * sinPi * Math.Sin(H),
            Math.Cos(dec) - rhoCosPhi * sinPi * Math.Cos(H)
        );

        double HPrime = H + deltaAlpha;

        double decPrime = Math.Atan2(
            (Math.Sin(dec) - rhoSinPhi * sinPi) * Math.Cos(deltaAlpha),
            Math.Cos(dec) - rhoCosPhi * sinPi * Math.Cos(H)
        );

        double sinAltitude =
            Math.Sin(phi) * Math.Sin(decPrime) + Math.Cos(phi) * Math.Cos(decPrime) * Math.Cos(HPrime);

        sinAltitude = Clamp(sinAltitude, -1.0, 1.0);

        double altitude = Math.Asin(sinAltitude);

        double azimuth = Math.Atan2(
            Math.Sin(HPrime),
            Math.Cos(HPrime) * Math.Sin(phi) - Math.Tan(decPrime) * Math.Cos(phi)
        );

        return new HorizontalCoordinates
        {
            Altitude = RadiansToDegrees(altitude),
            Azimuth = NormalizeDegrees(RadiansToDegrees(azimuth) + 180.0)
        };
    }

    // ============================================================
    // EQUATORIAL -> HORIZONTAL
    // ============================================================

    private HorizontalCoordinates EquatorialToHorizontal(
        double rightAscension,
        double declination,
        double latitude,
        double localSiderealTime
    )
    {
        double H = DegreesToRadians(
            NormalizeSignedDegrees(localSiderealTime - rightAscension)
        );

        double phi = DegreesToRadians(latitude);
        double dec = DegreesToRadians(declination);

        double sinAltitude =
            Math.Sin(phi) * Math.Sin(dec) + Math.Cos(phi) * Math.Cos(dec) * Math.Cos(H);

        sinAltitude = Clamp(sinAltitude, -1.0, 1.0);

        double altitude = Math.Asin(sinAltitude);

        double azimuth = Math.Atan2(
            Math.Sin(H),
            Math.Cos(H) * Math.Sin(phi) - Math.Tan(dec) * Math.Cos(phi)
        );

        return new HorizontalCoordinates
        {
            Altitude = RadiansToDegrees(altitude),
            Azimuth = NormalizeDegrees(RadiansToDegrees(azimuth) + 180.0)
        };
    }

    // ============================================================
    // SIDEREAL TIME
    // ============================================================

    private double CalculateLocalSiderealTime(double jd, double longitude)
    {
        double T = (jd - 2451545.0) / 36525.0;

        double gmst =
            280.46061837
            + 360.98564736629 * (jd - 2451545.0)
            + 0.000387933 * T * T
            - T * T * T / 38710000.0;

        return NormalizeDegrees(gmst + longitude);
    }

    // ============================================================
    // REFRACTION
    // ============================================================

    private double ApplyRefraction(double altitude)
    {
        /*
         * Bennett approximation.
         *
         * We intentionally do not extrapolate below -0.5°
         * because atmospheric refraction becomes unstable
         * near the mathematical horizon.
         */

        if (altitude <= -0.5 || altitude >= 90.0)
        {
            return altitude;
        }

        double denominator = altitude + 10.3 / (altitude + 5.11);

        if (Math.Abs(denominator) < 1e-9)
            return altitude;

        double correctionArcMinutes = 1.02 / Math.Tan(DegreesToRadians(denominator));

        return altitude + correctionArcMinutes / 60.0;
    }

    // ============================================================
    // HORIZONTAL -> UNITY
    // ============================================================

    private Vector3 HorizontalToUnity(double altitude, double azimuth)
    {
        double alt = DegreesToRadians(altitude);
        double azi = DegreesToRadians(azimuth);

        float x = (float)(Math.Cos(alt) * Math.Sin(azi));
        float y = (float)Math.Sin(alt);
        float z = (float)(Math.Cos(alt) * Math.Cos(azi));

        Vector3 direction = new Vector3(x, y, z);

        if (direction.sqrMagnitude < 1e-12f)
            return Vector3.forward;

        return direction.normalized;
    }

    // ============================================================
    // ROTATION
    // ============================================================

    private void SetCelestialRotation(Transform target, Vector3 direction, Vector3 offset)
    {
        if (target == null)
            return;

        target.rotation = Quaternion.LookRotation(-direction, Vector3.up) * Quaternion.Euler(offset);
    }

    // ============================================================
    // SHADER
    // ============================================================

    private void UpdateShaderGlobals()
    {
        if (!_UpdateShaderGlobals)
            return;

        Vector3 sunDirection = HorizontalToUnity(_SunAltitude, _SunAzimuth);
        Vector3 moonDirection = HorizontalToUnity(_MoonAltitude, _MoonAzimuth);

        SendShaderGlobals(sunDirection, moonDirection);
    }

    private void SendShaderGlobals(Vector3 sunDirection, Vector3 moonDirection)
    {
        Shader.SetGlobalVector(SunDirID, sunDirection);
        Shader.SetGlobalVector(MoonDirID, moonDirection);
        Shader.SetGlobalFloat(StarLatitudeID, (float)_Latitude);

        float normalizedLST = (float)(NormalizeDegrees(_LocalSiderealTime) / 360.0);

        Shader.SetGlobalFloat(StarSiderealTimeID, normalizedLST);

        if (_Moon != null)
        {
            Matrix4x4 moonSpace = new Matrix4x4(
                -_Moon.forward,
                _Moon.up,
                -_Moon.right,
                Vector4.zero
            ).transpose;

            Shader.SetGlobalMatrix(MoonSpaceMatrixID, moonSpace);
        }
    }

    // ============================================================
    // JULIAN DATE
    // ============================================================

    private double CalculateJulianDate(DateTime utc)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        int year = utc.Year;
        int month = utc.Month;

        double day =
            utc.Day
            + utc.Hour / 24.0
            + utc.Minute / 1440.0
            + utc.Second / 86400.0
            + utc.Millisecond / 86400000.0;

        if (month <= 2)
        {
            year--;
            month += 12;
        }

        int A = year / 100;
        int B = 2 - A + A / 4;

        return
            Math.Floor(365.25 * (year + 4716))
            + Math.Floor(30.6001 * (month + 1))
            + day + B - 1524.5;
    }

    // ============================================================
    // KEPLER
    // ============================================================

    private double SolveKepler(double meanAnomaly, double eccentricity)
    {
        double E = meanAnomaly;

        for (int i = 0; i < 12; i++)
        {
            double f = E - eccentricity * Math.Sin(E) - meanAnomaly;
            double derivative = 1.0 - eccentricity * Math.Cos(E);

            if (Math.Abs(derivative) < 1e-12)
                break;

            E -= f / derivative;
        }

        return E;
    }

    // ============================================================
    // DEBUG
    // ============================================================

    private void UpdateDebugInformation()
    {
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(_CurrentUTC, TimeZoneInfo.Local);
        TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(_CurrentUTC);

        _CurrentUTCTime = _CurrentUTC.ToString("yyyy-MM-dd HH:mm:ss.fff");
        _CurrentLocalTime = local.ToString("yyyy-MM-dd HH:mm:ss.fff");
        _DeviceTimeZone = TimeZoneInfo.Local.Id;
        _DeviceUTCOffsetHours = offset.TotalHours;
    }

    // ============================================================
    // MATH
    // ============================================================

    private static double NormalizeDegrees(double value)
    {
        value %= 360.0;

        if (value < 0.0)
            value += 360.0;

        return value;
    }

    private static double NormalizeSignedDegrees(double value)
    {
        value = NormalizeDegrees(value);

        if (value > 180.0)
            value -= 360.0;

        return value;
    }

    private static double NormalizeHours(double value)
    {
        value %= 24.0;

        if (value < 0.0)
            value += 24.0;

        return value;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static double RadiansToDegrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    // ============================================================
    // DATA
    // ============================================================

    private struct EquatorialCoordinates
    {
        public double RightAscension;
        public double Declination;
        public double DistanceEarthRadii;
    }

    private struct HorizontalCoordinates
    {
        public double Altitude;
        public double Azimuth;
    }

    // ============================================================
    // GIZMOS
    // ============================================================

    private void OnDrawGizmos()
    {
        if (_Sun != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, -_Sun.forward * 10.0f);
        }

        if (_Moon != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, -_Moon.forward * 10.0f);
        }
    }

#if UNITY_EDITOR

    // ============================================================
    // VALIDATION
    // ============================================================

    private void OnValidate()
    {
        _Latitude = Clamp(_Latitude, -90.0, 90.0);
        _Longitude = Clamp(_Longitude, -180.0, 180.0);
        _TimeScale = Math.Max(0.0, _TimeScale);
        _ManualUTCOffsetHours = Clamp(_ManualUTCOffsetHours, -14.0, 14.0);

        if (!_initialized)
            return;

        if (_TimeMode == TimeMode.ManualLocal)
        {
            _CurrentUTC = CreateManualLocalUTC();
            CacheState();
        }
        else if (_TimeMode == TimeMode.ManualUTC)
        {
            _CurrentUTC = CreateManualUTC();
            CacheState();
        }

        CalculateAstronomy();
        UpdateDebugInformation();
        UpdateShaderGlobals();

        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

#endif

    // ============================================================
    // INTERNAL FIELDS THAT MUST REMAIN AT CLASS LEVEL
    // ============================================================

    private double _SunGeometricAltitude;
    private double _MoonGeometricAltitude;
}