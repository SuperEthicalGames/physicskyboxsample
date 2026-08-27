#ifndef SKYBOX_FUNCTIONS_INCLUDED
#define SKYBOX_FUNCTIONS_INCLUDED

// From Inigo Quilez, https://www.iquilezles.org/www/articles/intersectors/intersectors.htm
void SphereIntersect_float(float3 RayDir, float3 SpherePos, float Radius,
    out float Distance, out float Hit)
{
    float3 oc = -SpherePos;

    float b = dot(oc, RayDir);
    float c = dot(oc, oc) - Radius * Radius;

    float h = b * b - c;

    if (h < 0.0)
    {
        Distance = -1.0;
        Hit = 0.0;
        return;
    }

    h = sqrt(h);

    Distance = -b - h;
    Hit = Distance > -1.0 ? 1.0 : 0.0;
}

// Found through trial and error resulting in mul(AngleAxis3x3(0.5*PI, float3(0,1,0)), AngleAxis3x3(-0.08*PI, float3(1,0,0)));
void MoonCorrection_float(
    float3 In,
    out float3 Out)
{
    float3x3 correctionMatrix = float3x3(
         0, -0.24869, 0.968583,
         0, 0.968583, 0.24869,
        -1, 0, 0
    );

    Out = mul(correctionMatrix, In);
}

// Rotation matrix
void AngleAxis3x3_float(
    float Angle, float3 Axis,
    out float3x3 Out)
{
    float c;
    float s;

    sincos(Angle, s, c);

    float t = 1.0 - c;

    float x = Axis.x;
    float y = Axis.y;
    float z = Axis.z;

    Out = float3x3(
        t * x * x + c,
        t * x * y - s * z,
        t * x * z + s * y,

        t * x * y + s * z,
        t * y * y + c,
        t * y * z - s * x,

        t * x * z - s * y,
        t * y * z + s * x,
        t * z * z + c
    );
}


// Rotate the view direction,
// tilt with latitude,
// spin with local sidereal time.
void GetStarUVW_float(
    float3 ViewDir, float Latitude, float LocalSiderealTime,
    out float3 StarUVW)
{
    // Tilt = 0 at the north pole
    // Latitude = 90
    float Tilt = PI * (Latitude - 90.0) / 180.0;

    float3x3 TiltRotation;

    AngleAxis3x3_float(Tilt, float3(1, 0, 0), TiltRotation);

    // 0.75 is the texture offset
    // so LST = 0 corresponds to noon.
    float Spin = (0.75 - LocalSiderealTime) * 2.0 * PI;

    float3x3 SpinRotation;

    AngleAxis3x3_float(Spin, float3(0, 1, 0), SpinRotation);

    // The order is important.
    float3x3 FullRotation = mul(SpinRotation, TiltRotation);

    StarUVW = mul(FullRotation, ViewDir);
}



float StarBlinkHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

void GetStarBlink_float(
    float3 StarUVW, float Time, float BlinkSpeed, float BlinkStrength, 
    out float Blink)
{
    float seedA = StarBlinkHash(StarUVW);
    float3 secondarySeedPosition = StarUVW.yzx + 17.31;
    float seedB = StarBlinkHash(secondarySeedPosition);

    float phase = seedA * 6.28318530718;
    float frequency = lerp(0.35, 1.5, seedB);

    float timeA = Time * BlinkSpeed * frequency + phase;
    float timeB = Time * BlinkSpeed * frequency * 1.73 + phase * 2.31;

    float blinkA = sin(timeA);
    float blinkB = sin(timeB);

    float blinkSignal = blinkA * 0.65 + blinkB * 0.35;

    blinkSignal = blinkSignal * 0.5 + 0.5;
    blinkSignal = saturate(blinkSignal);
    blinkSignal = pow(blinkSignal, 4.0);

    Blink = lerp(1.0, blinkSignal, BlinkStrength);
}

float GetConstellationBlinkValue(float Time, float BlinkSpeed, float BlinkStrength)
{
    float wave = sin(Time * BlinkSpeed);
    wave = wave * 0.5 + 0.5;
    return lerp(1.0, wave, BlinkStrength);
}

void GetConstellationBlink_float(
    float Time, float BlinkSpeed, float BlinkStrength, 
    out float Blink)
{
    Blink = GetConstellationBlinkValue(Time, BlinkSpeed, BlinkStrength);
}

#endif