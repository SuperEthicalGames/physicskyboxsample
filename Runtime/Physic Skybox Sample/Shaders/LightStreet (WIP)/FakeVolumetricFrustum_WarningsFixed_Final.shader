Shader "Custom/URP/FakeVolumetricCone_Recovered"
{
    Properties
    {
        // ================================================================
        // APARIENCIA
        // ================================================================

        [HDR]
        _ScatteringColor
        (
            "Scattering Color",
            Color
        ) = (1.0, 0.55, 0.12, 1)

        _Density
        (
            "Density",
            Range(0, 2)
        ) = 0.22

        _Intensity
        (
            "Intensity",
            Range(0, 8)
        ) = 2.5


        // ================================================================
        // NOISE
        // ================================================================

        _NoiseScale
        (
            "Noise Scale",
            Range(0.05, 6)
        ) = 1.5

        _NoiseStrength
        (
            "Noise Strength",
            Range(0, 1)
        ) = 0.30

        _NoiseSpeed
        (
            "Noise Speed",
            Range(-1, 1)
        ) = 0.08


        // ================================================================
        // VOLUMEN
        // ================================================================

        _Anisotropy
        (
            "Anisotropy",
            Range(-0.8, 0.8)
        ) = 0.45

        _HeightFalloff
        (
            "Height Falloff",
            Range(0.1, 4)
        ) = 0.7

        _EdgeSoftness
        (
            "Edge Softness",
            Range(0.001, 5)
        ) = 0.08


        // ================================================================
        // RAYMARCH
        // ================================================================

        _RaySteps
        (
            "Ray Steps",
            Range(4, 24)
        ) = 10

        _StepJitter
        (
            "Step Jitter",
            Range(0, 1)
        ) = 0.35

        _Absorption
        (
            "Absorption",
            Range(0, 2)
        ) = 0.15


        // ================================================================
        // CONO
        //
        // Y = 0      -> radio máximo
        // Y = Height -> apex
        // ================================================================

        _BaseRadius
        (
            "Base Radius",
            Float
        ) = 28.8

        _Height
        (
            "Height",
            Float
        ) = 17.79


        // ================================================================
        // NOISE WORLD
        // ================================================================

        _WorldNoiseScale
        (
            "World Noise Scale",
            Float
        ) = 1


        // ================================================================
        // LUZ ADICIONAL
        // ================================================================

        _AdditionalLightIndex
        (
            "Additional Light Index",
            Range(0, 7)
        ) = 0

        _PointLightBoost
        (
            "Point Light Boost",
            Range(0, 5)
        ) = 1.0


        // ================================================================
        // RENDER MODE
        //
        // 0 = OUTSIDE
        // 1 = INSIDE
        // ================================================================

        _RenderMode
        (
            "Render Mode",
            Range(0, 1)
        ) = 0
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }


        Pass
        {
            Name "FakeVolumetricCone"


            Blend SrcAlpha One

            ZWrite Off
            ZTest LEqual

            // Necesitamos ambas caras.
            //
            // OUTSIDE -> FRONT
            // INSIDE  -> BACK

            Cull Off


            HLSLPROGRAM


            #pragma vertex Vert
            #pragma fragment Frag

            #pragma target 3.5


            // ============================================================
            // LIGHTS
            // ============================================================

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP


            // ============================================================
            // MAIN LIGHT SHADOWS
            // ============================================================

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN


            // ============================================================
            // ADDITIONAL LIGHT SHADOWS
            // ============================================================

            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            // ============================================================
            // MATERIAL
            // ============================================================

            CBUFFER_START(UnityPerMaterial)

                float4 _ScatteringColor;

                float _Density;
                float _Intensity;

                float _NoiseScale;
                float _NoiseStrength;
                float _NoiseSpeed;

                float _Anisotropy;

                float _HeightFalloff;
                float _EdgeSoftness;

                float _RaySteps;
                float _StepJitter;
                float _Absorption;

                float _BaseRadius;
                float _Height;

                float _WorldNoiseScale;

                float _AdditionalLightIndex;
                float _PointLightBoost;

                float _RenderMode;

            CBUFFER_END


            // ============================================================
            // VERTEX
            // ============================================================

            struct Attributes
            {
                float3 positionOS : POSITION;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;

                float3 positionOS : TEXCOORD1;
            };


            Varyings Vert(
                Attributes IN
            )
            {
                Varyings OUT;


                OUT.positionOS =
                    IN.positionOS;


                OUT.positionWS =
                    TransformObjectToWorld(
                        IN.positionOS
                    );


                OUT.positionCS =
                    TransformWorldToHClip(
                        OUT.positionWS
                    );


                return OUT;
            }


            // ============================================================
            // CONE RADIUS
            //
            // r(y) = R * (1 - y/H)
            //
            // y = 0      -> R
            // y = H      -> 0
            // ============================================================

            float ConeRadiusAtHeight(
                float y
            )
            {
                float h =
                    saturate(
                        y /
                        max(
                            _Height,
                            0.0001
                        )
                    );


                return
                    _BaseRadius *
                    (
                        1.0 -
                        h
                    );
            }


            // ============================================================
            // HASH
            // ============================================================

            float Hash(
                float3 p
            )
            {
                p =
                    frac(
                        p *
                        0.3183099 +
                        0.1
                    );


                p *= 17.0;


                return frac(
                    p.x *
                    p.y *
                    p.z *
                    (
                        p.x +
                        p.y +
                        p.z
                    )
                );
            }


            // ============================================================
            // NOISE 3D
            // ============================================================

            float Noise3D(
                float3 p
            )
            {
                float3 i =
                    floor(p);


                float3 f =
                    frac(p);


                f =
                    f *
                    f *
                    (
                        3.0 -
                        2.0 * f
                    );


                float n000 =
                    Hash(
                        i +
                        float3(0, 0, 0)
                    );


                float n100 =
                    Hash(
                        i +
                        float3(1, 0, 0)
                    );


                float n010 =
                    Hash(
                        i +
                        float3(0, 1, 0)
                    );


                float n110 =
                    Hash(
                        i +
                        float3(1, 1, 0)
                    );


                float n001 =
                    Hash(
                        i +
                        float3(0, 0, 1)
                    );


                float n101 =
                    Hash(
                        i +
                        float3(1, 0, 1)
                    );


                float n011 =
                    Hash(
                        i +
                        float3(0, 1, 1)
                    );


                float n111 =
                    Hash(
                        i +
                        float3(1, 1, 1)
                    );


                float nx00 =
                    lerp(
                        n000,
                        n100,
                        f.x
                    );


                float nx10 =
                    lerp(
                        n010,
                        n110,
                        f.x
                    );


                float nx01 =
                    lerp(
                        n001,
                        n101,
                        f.x
                    );


                float nx11 =
                    lerp(
                        n011,
                        n111,
                        f.x
                    );


                float nxy0 =
                    lerp(
                        nx00,
                        nx10,
                        f.y
                    );


                float nxy1 =
                    lerp(
                        nx01,
                        nx11,
                        f.y
                    );


                return lerp(
                    nxy0,
                    nxy1,
                    f.z
                );
            }


            // ============================================================
            // CHEAP NOISE
            //
            // Solo 2 octavas.
            // ============================================================

            float CheapNoise(
                float3 p
            )
            {
                float n =
                    Noise3D(p) *
                    0.7;


                p *= 2.03;


                n +=
                    Noise3D(p) *
                    0.3;


                return n;
            }


            // ============================================================
            // DENSIDAD
            // ============================================================

            float SampleDensity(
                float3 p
            )
            {
                // --------------------------------------------------------
                // HEIGHT
                // --------------------------------------------------------

                float h =
                    saturate(
                        p.y /
                        max(
                            _Height,
                            0.0001
                        )
                    );


                // --------------------------------------------------------
                // NOISE
                // --------------------------------------------------------

                float3 noisePos =
                    p *
                    _NoiseScale *
                    _WorldNoiseScale;


                noisePos.y +=
                    _Time.y *
                    _NoiseSpeed;


                float noise =
                    CheapNoise(
                        noisePos
                    );


                noise =
                    saturate(
                        noise
                    );


                noise =
                    lerp(
                        1.0,
                        0.45 + noise,
                        _NoiseStrength
                    );


                // --------------------------------------------------------
                // VERTICAL DENSITY
                //
                // Esta es la distribución que daba el render anterior.
                // --------------------------------------------------------

                float vertical =
                    pow(
                        saturate(
                            1.0 - h
                        ),
                        _HeightFalloff
                    );


                vertical =
                    lerp(
                        0.45,
                        1.0,
                        vertical
                    );


                // --------------------------------------------------------
                // EDGE
                // --------------------------------------------------------

                float radius =
                    length(
                        p.xz
                    );


                float coneRadius =
                    max(
                        ConeRadiusAtHeight(
                            p.y
                        ),
                        0.001
                    );


                float normalizedEdge =
                    1.0 -
                    radius /
                    coneRadius;


                float edgeMask =
                    smoothstep(
                        0.0,
                        max(
                            _EdgeSoftness,
                            0.001
                        ),
                        normalizedEdge
                    );


                // --------------------------------------------------------
                // CENTER
                // --------------------------------------------------------

                float center =
                    saturate(
                        1.0 -
                        radius /
                        coneRadius
                    );


                center =
                    lerp(
                        0.75,
                        1.0,
                        center
                    );


                // --------------------------------------------------------
                // FINAL
                // --------------------------------------------------------

                return
                    _Density *
                    noise *
                    vertical *
                    edgeMask *
                    center;
            }


            // ============================================================
            // HENYEY-GREENSTEIN
            // ============================================================

            float PhaseHG(
                float cosTheta,
                float g
            )
            {
                float g2 =
                    g *
                    g;


                float d =
                    1.0 +
                    g2 -
                    2.0 *
                    g *
                    cosTheta;


                d =
                    max(
                        d,
                        0.001
                    );


                return
                    (
                        1.0 -
                        g2
                    )
                    /
                    (
                        4.0 *
                        3.14159265 *
                        pow(
                            d,
                            1.5
                        )
                    );
            }


            // ============================================================
            // FIND CONE EXIT
            //
            // Cono:
            //
            // r(y) = R * (1 - y/H)
            //
            // r(y) = R - k*y
            //
            // k = R/H
            //
            // Ecuación:
            //
            // x² + z² - (R - k*y)² = 0
            // ============================================================

            float FindConeExit(
                float3 p,
                float3 rd
            )
            {
                float H =
                    max(
                        _Height,
                        0.0001
                    );


                float R =
                    max(
                        _BaseRadius,
                        0.0001
                    );


                float k =
                    R /
                    H;


                // --------------------------------------------------------
                // QUADRATIC
                // --------------------------------------------------------

                float A =
                    rd.x *
                    rd.x
                    +
                    rd.z *
                    rd.z
                    -
                    k *
                    k *
                    rd.y *
                    rd.y;


                float B =
                    2.0 *
                    (
                        p.x *
                        rd.x
                        +
                        p.z *
                        rd.z
                        +
                        k *
                        rd.y *
                        (
                            R -
                            k *
                            p.y
                        )
                    );


                float coneRadius =
                    R -
                    k *
                    p.y;


                float C =
                    p.x *
                    p.x
                    +
                    p.z *
                    p.z
                    -
                    coneRadius *
                    coneRadius;


                float best =
                    1e20;


                // --------------------------------------------------------
                // NORMAL QUADRATIC
                // --------------------------------------------------------

                if (
                    abs(A) >
                    0.000001
                )
                {
                    float D =
                        B *
                        B
                        -
                        4.0 *
                        A *
                        C;


                    if (
                        D >= 0.0
                    )
                    {
                        float sqrtD =
                            sqrt(D);


                        float t0 =
                            (
                                -B -
                                sqrtD
                            )
                            /
                            (
                                2.0 *
                                A
                            );


                        float t1 =
                            (
                                -B +
                                sqrtD
                            )
                            /
                            (
                                2.0 *
                                A
                            );


                        // ------------------------------------------------
                        // FIRST INTERSECTION
                        // ------------------------------------------------

                        if (
                            t0 >
                            0.0001
                        )
                        {
                            float3 q0 =
                                p +
                                rd *
                                t0;


                            if (
                                q0.y >= 0.0 &&
                                q0.y <= H
                            )
                            {
                                best =
                                    min(
                                        best,
                                        t0
                                    );
                            }
                        }


                        // ------------------------------------------------
                        // SECOND INTERSECTION
                        // ------------------------------------------------

                        if (
                            t1 >
                            0.0001
                        )
                        {
                            float3 q1 =
                                p +
                                rd *
                                t1;


                            if (
                                q1.y >= 0.0 &&
                                q1.y <= H
                            )
                            {
                                best =
                                    min(
                                        best,
                                        t1
                                    );
                            }
                        }
                    }
                }
                else
                {
                    // ----------------------------------------------------
                    // LINEAR CASE
                    // ----------------------------------------------------

                    if (
                        abs(B) >
                        0.000001
                    )
                    {
                        float t =
                            -C /
                            B;


                        if (
                            t >
                            0.0001
                        )
                        {
                            float3 q =
                                p +
                                rd *
                                t;


                            if (
                                q.y >= 0.0 &&
                                q.y <= H
                            )
                            {
                                best =
                                    min(
                                        best,
                                        t
                                    );
                            }
                        }
                    }
                }


                // --------------------------------------------------------
                // BASE
                // --------------------------------------------------------

                if (
                    abs(rd.y) >
                    0.00001
                )
                {
                    float tBase =
                        -p.y /
                        rd.y;


                    if (
                        tBase >
                        0.0001
                    )
                    {
                        float3 qBase =
                            p +
                            rd *
                            tBase;


                        if (
                            length(
                                qBase.xz
                            )
                            <= R
                        )
                        {
                            best =
                                min(
                                    best,
                                    tBase
                                );
                        }
                    }
                }


                if (
                    best <
                    1e19
                )
                {
                    return best;
                }


                return -1.0;
            }


            // ============================================================
            // RANDOM
            // ============================================================

            float Random(
                float2 p
            )
            {
                return frac(
                    sin(
                        dot(
                            p,
                            float2(
                                12.9898,
                                78.233
                            )
                        )
                    )
                    *
                    43758.5453
                );
            }


            // ============================================================
            // RAYMARCH
            //
            // Esta es la parte que queremos recuperar sin modificaciones.
            // ============================================================

            half4 RayMarchSegment(
                float3 entryOS,
                float rayLength,
                float3 rayDirOS,
                float3 rayDirWS,
                float3 cameraWS,
                float3 fragmentWS,
                float2 screenUV
            )
            {
                if (
                    rayLength <=
                    0.01
                )
                {
                    return 0.0;
                }


                // --------------------------------------------------------
                // STEPS
                // --------------------------------------------------------

                int steps =
                    clamp(
                        (int)_RaySteps,
                        4,
                        24
                    );


                float stepSize =
                    rayLength /
                    steps;


                // --------------------------------------------------------
                // JITTER
                // --------------------------------------------------------

                float jitter =
                    Random(
                        screenUV
                    );


                float t =
                    stepSize *
                    jitter *
                    _StepJitter;


                // --------------------------------------------------------
                // LIGHT
                // --------------------------------------------------------

                Light mainLight =
                    GetMainLight();


                float3 scattering =
                    0.0;


                float transmittance =
                    1.0;


                // --------------------------------------------------------
                // VIEW DIRECTION
                // --------------------------------------------------------

                float3 viewDirWS =
                    normalize(
                        cameraWS -
                        fragmentWS
                    );


                // Evita warnings.
                viewDirWS += 0.0;


                // --------------------------------------------------------
                // RAYMARCH
                // --------------------------------------------------------

                [loop]
                for (
                    int i = 0;
                    i < 24;
                    i++
                )
                {
                    if (
                        i >= steps
                    )
                    {
                        break;
                    }


                    // ----------------------------------------------------
                    // SAMPLE
                    // ----------------------------------------------------

                    float3 sampleOS =
                        entryOS +
                        rayDirOS *
                        (
                            t +
                            stepSize *
                            0.5
                        );


                    // ----------------------------------------------------
                    // SAFETY
                    // ----------------------------------------------------

                    if (
                        sampleOS.y < 0.0 ||
                        sampleOS.y > _Height
                    )
                    {
                        t +=
                            stepSize;

                        continue;
                    }


                    // ----------------------------------------------------
                    // DENSITY
                    // ----------------------------------------------------

                    float density =
                        SampleDensity(
                            sampleOS
                        );


                    if (
                        density >
                        0.00001
                    )
                    {
                        float3 sampleWS =
                            TransformObjectToWorld(
                                sampleOS
                            );


                        // =================================================
                        // MAIN LIGHT
                        // =================================================

                        float3 mainLightDir =
                            normalize(
                                mainLight.direction
                            );


                        float mainCos =
                            dot(
                                -rayDirWS,
                                mainLightDir
                            );


                        float mainPhase =
                            PhaseHG(
                                mainCos,
                                _Anisotropy
                            );


                        float mainContribution =
                            mainLight.color.r +
                            mainLight.color.g +
                            mainLight.color.b;


                        mainContribution /=
                            3.0;


                        mainContribution *=
                            mainLight.shadowAttenuation;


                        // =================================================
                        // ADDITIONAL LIGHT
                        // =================================================

                        float3 localLight =
                            0.0;


                        #if defined(_ADDITIONAL_LIGHTS)

                            uint lightIndex =
                                (uint)
                                _AdditionalLightIndex;


                            Light additionalLight =
                                GetAdditionalLight(
                                    lightIndex,
                                    sampleWS
                                );


                            float3 toLight =
                                normalize(
                                    additionalLight.direction
                                );


                            float lightCos =
                                dot(
                                    -rayDirWS,
                                    toLight
                                );


                            float lightPhase =
                                PhaseHG(
                                    lightCos,
                                    _Anisotropy
                                );


                            float additionalIntensity =
                                (
                                    additionalLight.color.r +
                                    additionalLight.color.g +
                                    additionalLight.color.b
                                )
                                /
                                3.0;


                            localLight =
                                additionalLight.color *
                                lightPhase *
                                additionalLight.distanceAttenuation *
                                additionalLight.shadowAttenuation *
                                _PointLightBoost;

                        #endif


                        // =================================================
                        // SCATTERING
                        // =================================================

                        float3 light =
                            _ScatteringColor.rgb *
                            (
                                mainContribution *
                                mainPhase
                            );


                        light +=
                            _ScatteringColor.rgb *
                            localLight;


                        // =================================================
                        // INTEGRATE
                        // =================================================

                        float contribution =
                            density *
                            _Intensity *
                            stepSize;


                        scattering +=
                            light *
                            contribution *
                            transmittance;


                        // =================================================
                        // BEER-LAMBERT
                        // =================================================

                        transmittance *=
                            exp(
                                -density *
                                _Absorption *
                                stepSize
                            );
                    }


                    // ----------------------------------------------------
                    // EARLY EXIT
                    // ----------------------------------------------------

                    if (
                        transmittance <
                        0.02
                    )
                    {
                        break;
                    }


                    t +=
                        stepSize;
                }


                // --------------------------------------------------------
                // ALPHA
                // --------------------------------------------------------

                float alpha =
                    saturate(
                        1.0 -
                        transmittance
                    );


                return half4(
                    scattering,
                    alpha
                );
            }


            // ============================================================
            // OUTSIDE
            //
            // FRONT FACE = ENTRY
            //
            // ENTRY -> EXIT
            // ============================================================

            half4 RayMarchOutside(
                Varyings IN,
                float3 cameraWS,
                float3 rayDirWS,
                float3 rayDirOS
            )
            {
                float3 entryOS =
                    IN.positionOS;


                float exitT =
                    FindConeExit(
                        entryOS,
                        rayDirOS
                    );


                if (
                    exitT <
                    0.0
                )
                {
                    return 0.0;
                }


                return RayMarchSegment(
                    entryOS,
                    exitT,
                    rayDirOS,
                    rayDirWS,
                    cameraWS,
                    IN.positionWS,
                    IN.positionCS.xy
                );
            }


            // ============================================================
            // INSIDE
            //
            // CAMERA = ENTRY
            //
            // BACK FACE = EXIT
            //
            // CAMERA -> BACK
            // ============================================================

            half4 RayMarchInside(
                Varyings IN,
                float3 cameraWS,
                float3 cameraOS,
                float3 rayDirWS,
                float3 rayDirOS
            )
            {
                float3 entryOS =
                    cameraOS;


                float rayLength =
                    length(
                        IN.positionOS -
                        cameraOS
                    );


                if (
                    rayLength <=
                    0.01
                )
                {
                    return 0.0;
                }


                return RayMarchSegment(
                    entryOS,
                    rayLength,
                    rayDirOS,
                    rayDirWS,
                    cameraWS,
                    IN.positionWS,
                    IN.positionCS.xy
                );
            }


            // ============================================================
            // FRAGMENT
            //
            // BINARIO
            //
            // 0 = OUTSIDE
            // 1 = INSIDE
            // ============================================================

            half4 Frag(
                Varyings IN,
                bool frontFace : SV_IsFrontFace
            ) : SV_Target
            {
                // ========================================================
                // CAMERA
                // ========================================================

                float3 cameraWS =
                    GetCameraPositionWS();


                float3 cameraOS =
                    TransformWorldToObject(
                        cameraWS
                    );


                // ========================================================
                // CAMERA -> FRAGMENT
                // ========================================================

                float3 rayWS =
                    IN.positionWS -
                    cameraWS;


                float3 rayDirWS =
                    normalize(
                        rayWS
                    );


                float3 rayDirOS =
                    normalize(
                        TransformWorldToObjectDir(
                            rayDirWS
                        )
                    );


                // ========================================================
                // OUTSIDE
                // ========================================================

                if (
                    _RenderMode <
                    0.5
                )
                {
                    // ----------------------------------------------------
                    // Solo FRONT.
                    // ----------------------------------------------------

                    if (
                        !frontFace
                    )
                    {
                        discard;
                    }


                    return RayMarchOutside(
                        IN,
                        cameraWS,
                        rayDirWS,
                        rayDirOS
                    );
                }


                // ========================================================
                // INSIDE
                // ========================================================

                // --------------------------------------------------------
                // Solo BACK.
                // --------------------------------------------------------

                if (
                    frontFace
                )
                {
                    discard;
                }


                return RayMarchInside(
                    IN,
                    cameraWS,
                    cameraOS,
                    rayDirWS,
                    rayDirOS
                );
            }


            ENDHLSL
        }
    }
}