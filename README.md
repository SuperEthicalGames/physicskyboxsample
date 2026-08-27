# Physic Skybox Sample
<img width="903" height="508" alt="image" src="https://github.com/user-attachments/assets/5bf78bff-01fc-4259-94ce-2bef5b500937" />
<img width="905" height="506" alt="image" src="https://github.com/user-attachments/assets/e09b804b-131c-4723-906a-214c9dcc82b6" />

**Physic Skybox Sample** is an astronomical sky and day/night system for Unity designed to demonstrate a physically-inspired relationship between astronomical time, the Sun, the Moon, lighting, atmosphere, and sky rendering.

The package provides an astronomical time source that can drive the visual environment of a Unity scene, including the position of the Sun and Moon, daylight transitions, ambient lighting, fog, post-processing, stars, clouds, lunar illumination, and eclipse effects.

> **License:** Educational and non-commercial use only.  
> Copyright © 2026 Super Ethical Games Studio. All rights reserved.

---

## Features

### Astronomical Time System

`AstronomicalTimeSystem` is the central source of astronomical time and coordinates.

It supports four time modes:

- **RealTime** — uses the current UTC time.
- **ManualLocal** — evaluates a manually specified local date/time using the configured UTC offset.
- **ManualUTC** — evaluates a manually specified UTC date/time.
- **Simulated** — advances astronomical time using a configurable time scale.

The system supports:

- Latitude and longitude.
- Julian Date.
- Local Sidereal Time.
- Solar altitude and azimuth.
- Geometric and apparent solar altitude.
- Solar right ascension and declination.
- Lunar altitude and azimuth.
- Lunar distance.
- Lunar phase angle.
- Lunar illumination.
- Apparent solar time.
- Solar hour angle.
- Day/night factors.
- Twilight factors.
- Atmospheric refraction.
- Lunar topocentric correction.

The astronomical system is intended to act as the **single source of truth for time and celestial coordinates**. Visual systems should consume its results instead of maintaining independent clocks.

---

## Day and Night System

`DayNightCycleManager` converts astronomical information into visual environmental changes.

It can control:

- Sun color.
- Sun intensity.
- Sun visibility.
- Moon color.
- Moon intensity.
- Ambient light color.
- Ambient light intensity.
- Fog color.
- Fog density.
- URP Color Adjustments.
- Exposure.
- Saturation.
- Contrast.
- Bloom.
- Vignette.

Most visual properties are driven by `AnimationCurve` and `Gradient` assets, allowing the artistic presentation to be adjusted without modifying the astronomical calculations.

---

## Skybox

The package includes a URP-oriented astronomical skybox system containing resources for:

- Sun rendering.
- Moon rendering.
- Stars.
- Constellation figures.
- Clouds.
- Lunar eclipses.
- Solar eclipses.
- Atmospheric gradients.
- Skybox lighting.
- Custom shader functions.

`SkyboxController` communicates the current Sun and Moon directions to shaders through global shader properties.

---

## Included Samples

The package includes a complete demonstration scene showing the astronomical system working together with the skybox and environmental lighting.

The main sample scene is:

`Runtime/Physic Skybox Sample/Scene/Skybox Sample.unity`

The sample also contains:

- Skybox materials and shaders.
- Skybox Shader Graph assets.
- Cloud subgraphs.
- Sun and Moon subgraphs.
- Eclipse subgraphs.
- Star and constellation resources.
- Streetlight assets.
- Experimental volumetric-light shaders.
- URP volume configuration.
- Lighting settings.
- Terrain.

---

# Installation

Install the package through Unity's Package Manager using the package's Git URL or by placing the package inside the `Packages` directory of a Unity project.

The package identifier is:

`com.superethicalgamesstudio.physicskyboxsample`

After installation, open the included sample scene:

`Runtime/Physic Skybox Sample/Scene/Skybox Sample.unity`

---

# Basic Setup

## 1. Create the astronomical system

Add `AstronomicalTimeSystem` to a GameObject.

Assign:

- Sun Transform.
- Moon Transform.

Then configure the observer location:

- **Latitude**
- **Longitude**

Latitude uses:

- North = positive.
- South = negative.

Longitude uses:

- East = positive.
- West = negative.

For example:

```text
Latitude: 6.2442
Longitude: -75.5812
```

---

## 2. Select a time mode

### RealTime

Uses the computer's current UTC time.

Use this mode when the virtual environment should follow real astronomical time.

### ManualLocal

The configured date and time are interpreted as local time using `ManualUTCOffsetHours`.

Example:

```text
UTC Offset: -5
```

This can be used for locations such as Colombia.

### ManualUTC

The configured date and time are interpreted directly as UTC.

### Simulated

The system advances time according to `TimeScale`.

For example:

```text
TimeScale = 60
```

means approximately 60 astronomical seconds pass for every real second.

This is useful for:

- Demonstrations.
- Games.
- Time-lapse environments.
- Testing sunrise and sunset.
- Testing astronomical events.

---

# Day/Night Configuration

Add `DayNightCycleManager` to a GameObject and assign the corresponding:

- Astronomical Time System.
- Sun Light.
- Moon Light.
- Global URP Volume.

The manager uses the astronomical state to evaluate its configured gradients and curves.

For example, `SunDayFactor` maps approximately:

```text
-6° solar altitude  → 0
+6° solar altitude  → 1
```

This makes it possible to create smooth transitions through twilight and daylight.

---

# Public Astronomical API

`AstronomicalTimeSystem` exposes several values that can be consumed by other gameplay or rendering systems.

### Time

```csharp
CurrentUTC
CurrentTimeMode
TimeScale
```

### Observer

```csharp
Latitude
Longitude
ManualUTCOffsetHours
```

### Astronomical calculations

```csharp
JulianDate
LocalSiderealTime
```

### Sun

```csharp
SunAltitude
SunAzimuth
SunGeometricAltitude
SunRightAscension
SunDeclination
SunDayFactor
SunVisibility
```

### Moon

```csharp
MoonAltitude
MoonAzimuth
MoonGeometricAltitude
MoonDistanceEarthRadii
MoonPhaseAngle
MoonIllumination
```

### Solar time

```csharp
SolarHourAngle
SolarTimeHours
SolarCycle
```

### Environmental state

```csharp
DayFactor
NightFactor
TwilightFactor
IsDay
IsNight
```

---

# Architecture

The system is intentionally divided into three main responsibilities.

```text
AstronomicalTimeSystem
        │
        ├── Time
        ├── Sun
        ├── Moon
        ├── Solar calculations
        └── Shader globals
                 │
                 ▼
       DayNightCycleManager
                 │
        ├── Lighting
        ├── Ambient
        ├── Fog
        └── Post Processing
                 │
                 ▼
          Skybox / Shaders
```

This separation allows the astronomical calculations to remain independent from the artistic presentation.

The astronomical system determines **what is happening in the sky**.

The day/night manager determines **how the environment should look**.

The shaders determine **how the sky is rendered**.

---

# Shader Integration

The system can publish global shader parameters for celestial directions.

The skybox system uses parameters associated with:

```text
Sun direction
Moon direction
Moon-space matrix
Star latitude
Local sidereal time
```

This allows shaders to render celestial objects according to the astronomical state calculated by `AstronomicalTimeSystem`.

---

# Requirements

This package is intended for Unity:

**Unity 6.0.4 / 6000.4**

The runtime assembly references Unity's Universal Render Pipeline.

The package also declares the following test dependency:

```text
com.unity.test-framework 1.6.0
```

The included rendering assets require a project configured with URP.

---

# Package Structure

```text
com.superethicalgamesstudio.physicskyboxsample/
│
├── Documentation/
│
├── Editor/
│
├── Runtime/
│   └── Physic Skybox Sample/
│       ├── Prefabs/
│       │   └── Shaders/
│       ├── Scene/
│       └── Scripts/
│           ├── AstronomicalTimeSystem.cs
│           ├── DayNightCycleManager.cs
│           └── SkyboxController.cs
│
├── Samples/
│
├── Tests/
│
├── CHANGELOG.md
├── README.md
├── Third Party Notices.md
└── package.json
```

---

# Limitations

This package is intended as a **visual and educational astronomical system**, not as a professional scientific ephemeris library.

The astronomical calculations are designed for real-time rendering and game development.

Results may differ from high-precision astronomical software, particularly for long-term calculations, extreme dates, or applications requiring scientific-grade precision.

Rendering results also depend on:

- Unity version.
- URP configuration.
- Platform.
- Shader support.
- Scene lighting.
- Post-processing configuration.

---

# Educational Purpose

This package is provided to help developers, students, artists, and technical artists study:

- Astronomical coordinate systems.
- Solar and lunar positioning.
- Time systems.
- Day/night transitions.
- Procedural environmental lighting.
- Shader-driven sky rendering.
- Unity URP rendering.
- Real-time astronomical visualization.

---

# License

Copyright © 2026 **Super Ethical Games Studio**.

This software and its included original assets are provided under a **proprietary Educational Non-Commercial License**.

Permission is granted to use the package for:

- Personal learning.
- Educational projects.
- Academic research and coursework.
- Non-commercial experimentation.
- Non-commercial demonstrations.
- Portfolio projects that are not monetized.

The following uses are **not permitted** without explicit written permission from Super Ethical Games Studio:

- Commercial use.
- Selling the package or any substantial portion of it.
- Redistributing the package as a standalone asset.
- Including the package in a commercial product.
- Including the package in a paid game or application.
- Reselling modified versions.
- Publishing the package or substantial portions of it as another asset/package.
- Using the package as part of a commercial asset marketplace product.
- Removing or modifying copyright and license notices for redistribution.

No ownership rights are transferred by using this package.

All rights not expressly granted by this license are reserved by **Super Ethical Games Studio**.

For commercial licensing or other permissions, contact the copyright holder.

---

# Third-Party Content

Some files may depend on Unity technologies, Unity packages, or other third-party components.

Third-party components are **not automatically relicensed under this license**.

Their original licenses and terms remain applicable.

Users are responsible for complying with the licenses of third-party software, packages, textures, shaders, libraries, or other resources included in or required by their Unity project.

See:

`Third Party Notices.md`

for the third-party attribution information applicable to this package.

---

# Author

**Super Ethical Games Studio**

Package:

`com.superethicalgamesstudio.physicskyboxsample`

Version:

`0.1.0`

Copyright © 2026 Super Ethical Games Studio.

---

# Changelog

See `CHANGELOG.md` for release history.
