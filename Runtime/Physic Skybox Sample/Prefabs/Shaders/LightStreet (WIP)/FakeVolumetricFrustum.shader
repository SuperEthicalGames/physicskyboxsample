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

        _ViewScattering ("View Scattering", Range(0, 1)) = 0.0

        _HeightFalloff ("Height Falloff", Range(0.1, 4)) = 0.7

        _EdgeSoftness ("Edge Softness", Range(0.001, 5)) = 0.08

        _RaySteps ("Ray Steps", Range(4, 24)) = 10

        _StepJitter ("Step Jitter", Range(0, 1)) = 0.35

        _Absorption ("Absorption", Range(0, 2)) = 0.15

        _BaseRadius ("Base Radius", Float) = 28.8
        _TopRadius ("Top Radius", Float) = 0.5
        _Height ("Height", Float) = 17.79

        _WorldNoiseScale ("World Noise Scale", Float) = 1

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

            // ============================================================
            // IMPORTANTE
            //
            // Ya NO usamos LEqual.
            //
            // El depth test se hace manualmente usando
            // _CameraDepthTexture.
            // ============================================================

            Blend SrcAlpha One

            ZWrite Off
            ZTest Always

            Cull Off


            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma target 3.5

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            CBUFFER_START(UnityPerMaterial)

                float4 _ScatteringColor;

                float _Density;
                float _Intensity;

                float _NoiseScale;
                float _NoiseStrength;
                float _NoiseSpeed;

                float _Anisotropy;
                float _ViewScattering;

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
            // STRUCTURES
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



            // ============================================================
            // VERTEX
            // ============================================================

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
            // CONE
            // ============================================================

            float RadiusAtHeight(
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


                return lerp(
                    _BaseRadius,
                    _TopRadius,
                    h
                );
            }



            bool IsInsideCone(
                float3 p
            )
            {
                if (
                    p.y < 0.0 ||
                    p.y > _Height
                )
                {
                    return false;
                }


                float radius =
                    length(
                        p.xz
                    );


                float coneRadius =
                    RadiusAtHeight(
                        p.y
                    );


                return
                    radius <
                    coneRadius;
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
                        p * 0.3183099 +
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
                    f * f *
                    (
                        3.0 -
                        2.0 * f
                    );


                float n000 =
                    Hash(
                        i +
                        float3(0,0,0)
                    );


                float n100 =
                    Hash(
                        i +
                        float3(1,0,0)
                    );


                float n010 =
                    Hash(
                        i +
                        float3(0,1,0)
                    );


                float n110 =
                    Hash(
                        i +
                        float3(1,1,0)
                    );


                float n001 =
                    Hash(
                        i +
                        float3(0,0,1)
                    );


                float n101 =
                    Hash(
                        i +
                        float3(1,0,1)
                    );


                float n011 =
                    Hash(
                        i +
                        float3(0,1,1)
                    );


                float n111 =
                    Hash(
                        i +
                        float3(1,1,1)
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
            // DENSITY
            // ============================================================

            float SampleDensity(
                float3 p
            )
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
                // VERTICAL
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
                        RadiusAtHeight(
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
                    (
                        1.0 - g2
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
            // NORMALIZED PHASE
            // ============================================================

            float PhaseHGNormalized(
                float cosTheta,
                float g
            )
            {
                float phase =
                    PhaseHG(
                        cosTheta,
                        g
                    );


                return
                    phase *
                    (
                        4.0 *
                        3.14159265
                    );
            }



            // ============================================================
            // VIEW SCATTERING
            // ============================================================

            float GetViewPhase(
                float cosTheta
            )
            {
                float hg =
                    PhaseHGNormalized(
                        cosTheta,
                        _Anisotropy
                    );


                return lerp(
                    1.0,
                    hg,
                    _ViewScattering
                );
            }



            // ============================================================
            // FIND EXIT
            //
            // Encuentra dónde sale el rayo del cono.
            // Se utiliza especialmente cuando la cámara está dentro.
            // ============================================================

            float FindExit(
                float3 p,
                float3 rd
            )
            {
                float k =
                    (
                        _TopRadius -
                        _BaseRadius
                    )
                    /
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
                    )
                    *
                    (
                        r0 +
                        k * p.y
                    );


                if (
                    abs(A) >
                    0.000001
                )
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
                            (
                                -B - s
                            )
                            /
                            (
                                2.0 * A
                            );


                        float t1 =
                            (
                                -B + s
                            )
                            /
                            (
                                2.0 * A
                            );


                        float best =
                            1e20;


                        if (
                            t0 >
                            0.0001
                        )
                        {
                            float3 q0 =
                                p +
                                rd * t0;


                            if (
                                q0.y >= 0.0 &&
                                q0.y <= _Height
                            )
                            {
                                best =
                                    min(
                                        best,
                                        t0
                                    );
                            }
                        }


                        if (
                            t1 >
                            0.0001
                        )
                        {
                            float3 q1 =
                                p +
                                rd * t1;


                            if (
                                q1.y >= 0.0 &&
                                q1.y <= _Height
                            )
                            {
                                best =
                                    min(
                                        best,
                                        t1
                                    );
                            }
                        }


                        if (
                            best <
                            1e19
                        )
                        {
                            return best;
                        }
                    }
                }


                // --------------------------------------------------------
                // TOP CAP
                // --------------------------------------------------------

                if (
                    abs(rd.y) >
                    0.00001
                )
                {
                    float tTop =
                        (
                            _Height -
                            p.y
                        )
                        /
                        rd.y;


                    if (
                        tTop >
                        0.0001
                    )
                    {
                        float3 qTop =
                            p +
                            rd * tTop;


                        if (
                            length(
                                qTop.xz
                            )
                            <= _TopRadius
                        )
                        {
                            return tTop;
                        }
                    }


                    // ----------------------------------------------------
                    // BOTTOM CAP
                    // ----------------------------------------------------

                    float tBottom =
                        -p.y /
                        rd.y;


                    if (
                        tBottom >
                        0.0001
                    )
                    {
                        float3 qBottom =
                            p +
                            rd * tBottom;


                        if (
                            length(
                                qBottom.xz
                            )
                            <= _BaseRadius
                        )
                        {
                            return tBottom;
                        }
                    }
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
            // SCENE DEPTH
            //
            // Devuelve:
            //
            //   distancia desde la cámara hasta la primera superficie
            //   opaca de la escena sobre este pixel.
            //
            // Si vemos el skybox:
            //
            //   devuelve un número enorme.
            // ============================================================

            float GetSceneRayDistance(
                float4 positionCS,
                float3 cameraWS,
                float3 rayDirWS
            )
            {
                float2 screenUV =
                    positionCS.xy /
                    _ScaledScreenParams.xy;


                float rawDepth =
                    SampleSceneDepth(
                        screenUV
                    );


                // --------------------------------------------------------
                // SKY / FAR
                // --------------------------------------------------------

                #if UNITY_REVERSED_Z

                    if (
                        rawDepth <=
                        0.00001
                    )
                    {
                        return 1e20;
                    }

                #else

                    if (
                        rawDepth >=
                        0.99999
                    )
                    {
                        return 1e20;
                    }

                #endif


                // --------------------------------------------------------
                // Convert depth to NDC.
                //
                // Esto sigue el procedimiento de URP.
                // --------------------------------------------------------

                float depthNDC =
                    rawDepth;


                #if !UNITY_REVERSED_Z

                    depthNDC =
                        lerp(
                            UNITY_NEAR_CLIP_VALUE,
                            1.0,
                            rawDepth
                        );

                #endif


                // --------------------------------------------------------
                // RECONSTRUCT WORLD POSITION
                // --------------------------------------------------------

                float3 sceneWS =
                    ComputeWorldSpacePosition(
                        screenUV,
                        depthNDC,
                        UNITY_MATRIX_I_VP
                    );


                // --------------------------------------------------------
                // Proyectamos el punto de profundidad sobre el rayo.
                //
                // Esto es más robusto que usar simplemente distance().
                // --------------------------------------------------------

                float sceneRayDistance =
                    dot(
                        sceneWS -
                        cameraWS,
                        rayDirWS
                    );


                return
                    max(
                        sceneRayDistance,
                        0.0
                    );
            }



            // ============================================================
            // FRAGMENT
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
                // CAMERA -> PIXEL
                // ========================================================

                float3 rayWS =
                    IN.positionWS -
                    cameraWS;


                float fragmentDistance =
                    length(
                        rayWS
                    );


                if (
                    fragmentDistance <=
                    0.0001
                )
                {
                    discard;
                }


                float3 rayDirWS =
                    rayWS /
                    fragmentDistance;


                float3 rayDirOS =
                    normalize(
                        TransformWorldToObjectDir(
                            rayDirWS
                        )
                    );


                // ========================================================
                // CAMERA INSIDE
                // ========================================================

                bool cameraInside =
                    IsInsideCone(
                        cameraOS
                    );


                // ========================================================
                // FACE SELECTION
                //
                // FUERA:
                //     necesitamos la cara frontal.
                //
                // DENTRO:
                //     necesitamos la cara trasera.
                //
                // ========================================================

                if (!cameraInside)
                {
                    if (!frontFace)
                    {
                        discard;
                    }
                }
                else
                {
                    if (frontFace)
                    {
                        discard;
                    }
                }


                // ========================================================
                // SCENE DEPTH
                // ========================================================

                float sceneRayDistance =
                    GetSceneRayDistance(
                        IN.positionCS,
                        cameraWS,
                        rayDirWS
                    );


                // ========================================================
                // RAY SEGMENT
                // ========================================================

                float3 entryOS;

                float rayLength;


                if (cameraInside)
                {
                    // ====================================================
                    // CAMERA DENTRO
                    //
                    // El volumen empieza exactamente en la cámara.
                    //
                    // El final será:
                    //
                    //   salida del cono
                    //              O
                    //   primer objeto de escena
                    //
                    // el que esté primero.
                    // ====================================================

                    entryOS =
                        cameraOS;


                    float coneExit =
                        FindExit(
                            cameraOS,
                            rayDirOS
                        );


                    if (
                        coneExit <=
                        0.0001
                    )
                    {
                        discard;
                    }


                    rayLength =
                        coneExit;


                    // ----------------------------------------------------
                    // Si hay un objeto antes de salir del cono,
                    // terminamos el raymarch en ese objeto.
                    //
                    // IMPORTANTE:
                    //
                    // NO hacemos discard aquí.
                    //
                    // Queremos que el volumen existente ENTRE LA CÁMARA
                    // y el objeto pueda seguir apareciendo sobre el objeto.
                    // ----------------------------------------------------

                    rayLength =
                        min(
                            rayLength,
                            sceneRayDistance
                        );
                }
                else
                {
                    // ====================================================
                    // CAMERA FUERA
                    //
                    // El fragmento representa la entrada del cono.
                    // ====================================================

                    entryOS =
                        IN.positionOS;


                    float entryDistance =
                        fragmentDistance;


                    float exitT =
                        FindExit(
                            entryOS,
                            rayDirOS
                        );


                    if (
                        exitT <=
                        0.0001
                    )
                    {
                        discard;
                    }


                    // ----------------------------------------------------
                    // Si un objeto está antes de la entrada del cono,
                    // entonces el volumen completo está oculto.
                    // ----------------------------------------------------

                    if (
                        sceneRayDistance <
                        entryDistance
                    )
                    {
                        discard;
                    }


                    rayLength =
                        exitT;


                    // ----------------------------------------------------
                    // De lo contrario, el objeto puede cortar el volumen.
                    // ----------------------------------------------------

                    float volumeDistance =
                        entryDistance +
                        exitT;


                    float maxFromEntry =
                        sceneRayDistance -
                        entryDistance;


                    if (
                        sceneRayDistance <
                        volumeDistance
                    )
                    {
                        rayLength =
                            min(
                                rayLength,
                                maxFromEntry
                            );
                    }
                }


                // ========================================================
                // VALIDATE RAY
                // ========================================================

                if (
                    rayLength <=
                    0.01
                )
                {
                    discard;
                }


                // ========================================================
                // STEPS
                // ========================================================

                int steps =
                    clamp(
                        (int)_RaySteps,
                        4,
                        24
                    );


                float stepSize =
                    rayLength /
                    steps;


                if (
                    stepSize <=
                    0.00001
                )
                {
                    discard;
                }


                // ========================================================
                // JITTER
                // ========================================================

                float jitter =
                    Random(
                        IN.positionCS.xy
                    );


                float t =
                    stepSize *
                    jitter *
                    _StepJitter;


                // ========================================================
                // MAIN LIGHT
                // ========================================================

                Light mainLight =
                    GetMainLight();


                // ========================================================
                // ACCUMULATION
                // ========================================================

                float3 scattering =
                    0.0;


                float transmittance =
                    1.0;


                // ========================================================
                // RAYMARCH
                // ========================================================

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
                    // SAMPLE POSITION
                    // ----------------------------------------------------

                    float sampleT =
                        t +
                        stepSize *
                        0.5;


                    float3 sampleOS =
                        entryOS +
                        rayDirOS *
                        sampleT;


                    // ----------------------------------------------------
                    // HEIGHT SAFETY
                    // ----------------------------------------------------

                    if (
                        sampleOS.y < 0.0 ||
                        sampleOS.y > _Height
                    )
                    {
                        t += stepSize;
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
                            GetViewPhase(
                                mainCos
                            );


                        float mainContribution =
                            (
                                mainLight.color.r +
                                mainLight.color.g +
                                mainLight.color.b
                            )
                            /
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
                            (uint)_AdditionalLightIndex;


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
                            GetViewPhase(
                                lightCos
                            );


                        localLight =
                            additionalLight.color *
                            lightPhase *
                            additionalLight.distanceAttenuation *
                            additionalLight.shadowAttenuation *
                            _PointLightBoost;

                        #endif


                        // =================================================
                        // COMBINE
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


                // ========================================================
                // ALPHA
                // ========================================================

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
