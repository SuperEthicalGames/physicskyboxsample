Shader "Custom/URP/FakeVolumetricFrustum"
{
    Properties
    {
        [HDR]
        _ScatteringColor ("Scattering Color", Color) = (1.0, 0.55, 0.12, 1)

        _Density ("Density", Range(0, 2)) = 0.22
        _Intensity ("Intensity", Range(0, 8)) = 2.5

        _NoiseScale ("Noise Scale", Range(0.05, 6)) = 1.5
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.30
        _NoiseSpeed ("Noise Speed", Range(-1, 1)) = 0.08

        _Anisotropy ("Anisotropy", Range(-0.8, 0.8)) = 0.45

        _HeightFalloff ("Height Falloff", Range(0.1, 4)) = 0.7

        // Ahora es porcentaje del radio.
        // 0.05 = 5% del radio.
        _EdgeSoftness ("Edge Softness", Range(0.001, 5)) = 0.08

        _RaySteps ("Ray Steps", Range(4, 24)) = 10

        _StepJitter ("Step Jitter", Range(0, 1)) = 0.35

        _Absorption ("Absorption", Range(0, 2)) = 0.15

        _BaseRadius ("Base Radius", Float) = 28.8
        _TopRadius ("Top Radius", Float) = 0.5
        _Height ("Height", Float) = 17.79

        _WorldNoiseScale ("World Noise Scale", Float) = 1

        // Control de la luz adicional.
        // Por defecto usamos la primera luz adicional.
        _AdditionalLightIndex ("Additional Light Index", Range(0, 7)) = 0

        _PointLightBoost ("Point Light Boost", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "FakeVolumetric"

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual

            // NECESARIO:
            // fuera -> vemos cara frontal
            // dentro -> vemos cara trasera
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma target 3.5

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

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
                float _TopRadius;
                float _Height;

                float _WorldNoiseScale;

                float _AdditionalLightIndex;
                float _PointLightBoost;

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


            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionOS = IN.positionOS;

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
            // CONE
            // ============================================================

            float RadiusAtHeight(float y)
            {
                float h =
                    saturate(
                        y /
                        max(_Height, 0.0001)
                    );

                return lerp(
                    _BaseRadius,
                    _TopRadius,
                    h
                );
            }


            bool IsInsideCone(float3 p)
            {
                if (p.y < 0.0 ||
                    p.y > _Height)
                {
                    return false;
                }

                float radius =
                    length(p.xz);

                float coneRadius =
                    RadiusAtHeight(p.y);

                return radius <
                       coneRadius;
            }


            // ============================================================
            // HASH / NOISE
            // ============================================================

            float Hash(float3 p)
            {
                p =
                    frac(
                        p * 0.3183099 +
                        0.1
                    );

                p *= 17.0;

                return frac(
                    p.x *
                    p.y *
                    p.z *
                    (p.x + p.y + p.z)
                );
            }


            float Noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                f =
                    f * f *
                    (3.0 - 2.0 * f);

                float n000 =
                    Hash(i + float3(0,0,0));

                float n100 =
                    Hash(i + float3(1,0,0));

                float n010 =
                    Hash(i + float3(0,1,0));

                float n110 =
                    Hash(i + float3(1,1,0));

                float n001 =
                    Hash(i + float3(0,0,1));

                float n101 =
                    Hash(i + float3(1,0,1));

                float n011 =
                    Hash(i + float3(0,1,1));

                float n111 =
                    Hash(i + float3(1,1,1));

                float nx00 =
                    lerp(n000, n100, f.x);

                float nx10 =
                    lerp(n010, n110, f.x);

                float nx01 =
                    lerp(n001, n101, f.x);

                float nx11 =
                    lerp(n011, n111, f.x);

                float nxy0 =
                    lerp(nx00, nx10, f.y);

                float nxy1 =
                    lerp(nx01, nx11, f.y);

                return lerp(
                    nxy0,
                    nxy1,
                    f.z
                );
            }


            // ============================================================
            // 2 OCTAVES ONLY
            //
            // Much cheaper than previous FBM.
            // ============================================================

            float CheapNoise(float3 p)
            {
                float n =
                    Noise3D(p) * 0.7;

                p *= 2.03;

                n +=
                    Noise3D(p) * 0.3;

                return n;
            }


            // ============================================================
            // DENSITY
            // ============================================================

            float SampleDensity(float3 p)
            {
                float h =
                    saturate(
                        p.y /
                        max(
                            _Height,
                            0.0001
                        )
                    );

                // --------------------------------------------------------
                // Noise
                // --------------------------------------------------------

                float3 noisePos =
                    p *
                    _NoiseScale *
                    _WorldNoiseScale;

                noisePos.y +=
                    _Time.y *
                    _NoiseSpeed;

                float noise =
                    CheapNoise(noisePos);

                noise =
                    saturate(noise);

                noise =
                    lerp(
                        1.0,
                        0.45 +
                        noise,
                        _NoiseStrength
                    );

                // --------------------------------------------------------
                // Vertical density
                // --------------------------------------------------------

                float vertical =
                    pow(
                        saturate(1.0 - h),
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
                //
                // Ahora EdgeSoftness es NORMALIZADO.
                //
                // 0.08 significa:
                // últimos 8% del radio -> fade.
                // --------------------------------------------------------

                float radius =
                    length(p.xz);

                float coneRadius =
                    max(
                        RadiusAtHeight(p.y),
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
                // Centro ligeramente más denso.
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

                return
                    _Density *
                    noise *
                    vertical *
                    edgeMask *
                    center;
            }


            // ============================================================
            // HG PHASE
            // ============================================================

            float PhaseHG(
                float cosTheta,
                float g)
            {
                float g2 =
                    g * g;

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
                    (1.0 - g2) /
                    (
                        4.0 *
                        3.14159265 *
                        pow(d, 1.5)
                    );
            }


            // ============================================================
            // FIND EXIT
            //
            // El punto recibido es un punto de la superficie.
            // Buscamos la siguiente intersección.
            // ============================================================

            bool FindExit(
                float3 p,
                float3 rd,
                out float tExit)
            {
                tExit = 0.0;

                float k =
                    (
                        _TopRadius -
                        _BaseRadius
                    ) /
                    max(
                        _Height,
                        0.0001
                    );

                float r0 =
                    _BaseRadius;

                float A =
                    rd.x * rd.x +
                    rd.z * rd.z -
                    k * k *
                    rd.y * rd.y;

                float B =
                    2.0 *
                    (
                        p.x * rd.x +
                        p.z * rd.z -
                        k * rd.y *
                        (
                            r0 +
                            k * p.y
                        )
                    );

                float C =
                    p.x * p.x +
                    p.z * p.z -
                    (
                        r0 +
                        k * p.y
                    ) *
                    (
                        r0 +
                        k * p.y
                    );

                if (abs(A) > 0.000001)
                {
                    float D =
                        B * B -
                        4.0 *
                        A *
                        C;

                    if (D >= 0.0)
                    {
                        float s =
                            sqrt(D);

                        float t0 =
                            (-B - s) /
                            (2.0 * A);

                        float t1 =
                            (-B + s) /
                            (2.0 * A);

                        float best =
                            1e20;

                        if (t0 > 0.0001)
                        {
                            float3 q =
                                p +
                                rd * t0;

                            if (q.y >= 0.0 &&
                                q.y <= _Height)
                            {
                                best =
                                    min(
                                        best,
                                        t0
                                    );
                            }
                        }

                        if (t1 > 0.0001)
                        {
                            float3 q =
                                p +
                                rd * t1;

                            if (q.y >= 0.0 &&
                                q.y <= _Height)
                            {
                                best =
                                    min(
                                        best,
                                        t1
                                    );
                            }
                        }

                        if (best < 1e19)
                        {
                            tExit =
                                best;

                            return true;
                        }
                    }
                }

                // --------------------------------------------------------
                // Top cap intersection.
                // --------------------------------------------------------

                if (abs(rd.y) > 0.00001)
                {
                    float tTop =
                        (
                            _Height -
                            p.y
                        ) /
                        rd.y;

                    if (tTop > 0.0001)
                    {
                        float3 q =
                            p +
                            rd * tTop;

                        if (
                            length(q.xz) <=
                            _TopRadius
                        )
                        {
                            tExit =
                                tTop;

                            return true;
                        }
                    }

                    // ----------------------------------------------------
                    // Bottom cap intersection.
                    // ----------------------------------------------------

                    float tBottom =
                        -p.y /
                        rd.y;

                    if (tBottom > 0.0001)
                    {
                        float3 q =
                            p +
                            rd * tBottom;

                        if (
                            length(q.xz) <=
                            _BaseRadius
                        )
                        {
                            tExit =
                                tBottom;

                            return true;
                        }
                    }
                }

                return false;
            }


            // ============================================================
            // RANDOM
            // ============================================================

            float Random(float2 p)
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
                    ) *
                    43758.5453
                );
            }


            // ============================================================
            // MAIN FRAGMENT
            // ============================================================

            half4 Frag(
                Varyings IN,
                bool frontFace : SV_IsFrontFace
            ) : SV_Target
            {
                // --------------------------------------------------------
                // Camera
                // --------------------------------------------------------

                float3 cameraWS =
                    GetCameraPositionWS();

                float3 cameraOS =
                    TransformWorldToObject(
                        cameraWS
                    );

                // --------------------------------------------------------
                // Camera -> fragment ray
                // --------------------------------------------------------

                float3 rayWS =
                    IN.positionWS -
                    cameraWS;

                float3 rayDirWS =
                    normalize(rayWS);

                float3 rayDirOS =
                    normalize(
                        TransformWorldToObjectDir(
                            rayDirWS
                        )
                    );

                // --------------------------------------------------------
                // URP light-loop input.
                // Forward+ / Cluster Light Loop requires this struct
                // to exist in the LIGHT_LOOP_BEGIN scope.
                // --------------------------------------------------------

                InputData inputData =
                    (InputData)0;

                inputData.positionWS =
                    IN.positionWS;

                inputData.normalWS =
                    float3(0.0, 1.0, 0.0);

                inputData.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(
                        IN.positionWS
                    );

                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(
                        IN.positionCS
                    );

                // --------------------------------------------------------
                // Is camera inside volume?
                // --------------------------------------------------------

                bool cameraInside =
                    IsInsideCone(
                        cameraOS
                    );

                // --------------------------------------------------------
                // FRONT/BACK SELECTION
                //
                // Outside:
                //     render FRONT face.
                //
                // Inside:
                //     render BACK face.
                // --------------------------------------------------------

                if (!cameraInside)
                {
                    if (!frontFace)
                        discard;
                }
                else
                {
                    if (frontFace)
                        discard;
                }

                // --------------------------------------------------------
                // Determine ray segment.
                // --------------------------------------------------------

                float3 entryOS;
                float rayLength;

                if (cameraInside)
                {
                    // ----------------------------------------------------
                    // CAMERA IS ALREADY INSIDE
                    //
                    // Camera -> back wall.
                    // The current mesh fragment is the EXIT.
                    // ----------------------------------------------------

                    entryOS =
                        cameraOS;

                    rayLength =
                        length(
                            IN.positionOS -
                            cameraOS
                        );
                }
                else
                {
                    // ----------------------------------------------------
                    // CAMERA OUTSIDE
                    //
                    // Current surface = ENTRY.
                    // Find the EXIT.
                    // ----------------------------------------------------

                    entryOS =
                        IN.positionOS;

                    float exitT = 0.0;

                    if (!FindExit(
                            entryOS,
                            rayDirOS,
                            exitT))
                    {
                        discard;
                    }

                    rayLength =
                        exitT;
                }

                if (rayLength <= 0.01)
                    discard;

                // --------------------------------------------------------
                // Steps
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
                // Jitter
                // --------------------------------------------------------

                float jitter =
                    Random(
                        IN.positionCS.xy
                    );

                float t =
                    stepSize *
                    jitter *
                    _StepJitter;

                // --------------------------------------------------------
                // Light
                // --------------------------------------------------------

                Light mainLight =
                    GetMainLight();

                // --------------------------------------------------------
                // We use the first additional light if available.
                //
                // This is important because your lamp appears to be
                // a Point Light, not the Main Directional Light.
                // --------------------------------------------------------

                float3 scattering =
                    0.0;

                float transmittance =
                    1.0;

                // --------------------------------------------------------
                // View direction
                // --------------------------------------------------------

                float3 viewDirWS =
                    normalize(
                        cameraWS -
                        IN.positionWS
                    );

                // --------------------------------------------------------
                // Raymarch
                // --------------------------------------------------------

                [loop]
                for (int i = 0; i < 24; i++)
                {
                    if (i >= steps)
                        break;

                    float3 sampleOS =
                        entryOS +
                        rayDirOS *
                        (
                            t +
                            stepSize * 0.5
                        );

                    // ----------------------------------------------------
                    // Safety
                    // ----------------------------------------------------

                    if (
                        sampleOS.y < 0.0 ||
                        sampleOS.y > _Height
                    )
                    {
                        t += stepSize;
                        continue;
                    }

                    float density =
                        SampleDensity(
                            sampleOS
                        );

                    if (density > 0.00001)
                    {
                        float3 sampleWS =
                            TransformObjectToWorld(
                                sampleOS
                            );

                        // ------------------------------------------------
                        // Main directional light
                        // ------------------------------------------------

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

                        // ------------------------------------------------
                        // Additional Point/Spot light
                        // ------------------------------------------------

                        float3 localLight =
                            0.0;

                        #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP

                        uint targetLightIndex =
                            (uint)_AdditionalLightIndex;

                        // Forward+ requires InputData to be in scope for
                        // LIGHT_LOOP_BEGIN.
                        uint pixelLightCount =
                            GetAdditionalLightsCount();

                        LIGHT_LOOP_BEGIN(pixelLightCount)

                            if (lightIndex == targetLightIndex)
                            {
                                Light additionalLight =
                                    (Light)0;

                                additionalLight =
                                    GetAdditionalLight(
                                        lightIndex,
                                        inputData.positionWS,
                                        half4(1, 1, 1, 1)
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

                                localLight =
                                    additionalLight.color *
                                    lightPhase *
                                    additionalLight.distanceAttenuation *
                                    additionalLight.shadowAttenuation *
                                    _PointLightBoost;
                            }

                        LIGHT_LOOP_END

                        #endif

                        // ------------------------------------------------
                        // Ambient/main contribution
                        // ------------------------------------------------

                        float3 light =
                            _ScatteringColor.rgb *
                            (
                                mainContribution *
                                mainPhase
                            );

                        // ------------------------------------------------
                        // Add point light.
                        // ------------------------------------------------

                        light +=
                            _ScatteringColor.rgb *
                            localLight;

                        // ------------------------------------------------
                        // Integrate
                        // ------------------------------------------------

                        float contribution =
                            density *
                            _Intensity *
                            stepSize;

                        scattering +=
                            light *
                            contribution *
                            transmittance;

                        // ------------------------------------------------
                        // Beer-Lambert
                        // ------------------------------------------------

                        transmittance *=
                            exp(
                                -density *
                                _Absorption *
                                stepSize
                            );
                    }

                    if (transmittance < 0.02)
                        break;

                    t += stepSize;
                }

                // --------------------------------------------------------
                // Alpha based on accumulated extinction.
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

            ENDHLSL
        }
    }
}