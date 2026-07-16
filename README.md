# Unity Tools

A modular set of drop-in tools and services for any Unity project.

- **Package:** `com.kaddumi.unity-tools`
- **Version:** `0.1.0`
- **Unity:** `6000.3` or newer
- **Dependencies:** `com.unity.ugui` (2.0.0)
- **Author:** [Qasim Kaddumi](https://qasimkaddumi.github.io/portfolio/)

The package ships as two assemblies:

| Assembly | Location | Purpose |
| --- | --- | --- |
| `KaddumiUnityTools.Runtime` | `Runtime/` | Gameplay/runtime services (ads, analytics, auth, consent, loading, events, service locator). |
| `KaddumiUnityTools.Editor` | `Editor/` | Editor-only tooling (folder generation, SO reset, project icons, iOS post-build). |

---

## Installation

Add the package to your project via the Package Manager (`Add package from git URL...`) or by referencing it in `Packages/manifest.json`. Once imported, both assemblies are picked up automatically.

Most systems are built around **ScriptableObject providers**: you assign a provider asset in the inspector to choose which backend/SDK a given manager uses, keeping game code SDK-agnostic.

---

## Runtime Tools

### Service Locator
`Runtime/Serivces_System/ServiceLocator.cs`

A lightweight, `DontDestroyOnLoad` singleton that resolves services implementing `IService`. `GetService<T>()` looks for the component on the ServiceLocator GameObject, then falls back to its children. Managers below implement `IService` so they can be located and initialized centrally.

### Ads System
`Runtime/Ads_System/`

SDK-agnostic ad management via the `IAdProvider` interface and `AdProviderSO` provider assets.

- **`AdManager`** — singleton `MonoBehaviour` (`IService`) that loads and shows interstitial and rewarded ads, tracks reward callbacks, and honors consent before serving personalized ads.
- **Providers:** `AdMobProvider` / `AdMobProviderSO` (Google Mobile Ads, with AppLovin, Liftoff/Vungle and Unity Ads mediation adapters).
- **Core:** `AdType`, `AdReward`, `AdErrorDomain`; config via `AdConfig`.

### Analytics System
`Runtime/Analytics_System/`

Multi-backend analytics — all assigned providers receive every event.

- **`AnalyticsManager`** — singleton `MonoBehaviour` (`IService`) owning an `AnalyticsService`; accepts a list of `AnalyticsProviderSO` assets.
- **Providers:** `FirebaseAnalyticsProvider` / `FirebaseAnalyticsProviderSO`, and `DebugLoggerProvider` / `DebugLoggerProviderSO` for editor/testing.

### Authentication System
`Runtime/Authentication_System/`

SDK-agnostic sign-in, structurally mirroring the analytics manager. Each request is routed to a provider that supports the requested `AuthMethod`.

- **`AuthManager`** — singleton `MonoBehaviour` (`IService`) owning an `AuthService`; exposes `CurrentUser`, `IsSignedIn`, and `OnSignedIn` / `OnSignedOut` events. Optional shared `AuthConfig`.
- **Providers:** `FirebaseAuthProvider` / `FirebaseAuthProviderSO`, and `DebugAuthProvider` / `DebugAuthProviderSO`.
- **Core:** `AuthCredentials`, `AuthResult`, `AuthUser`, `AuthError`, `AuthMethod`.

### Consent Service
`Runtime/ConsentService/`

Central privacy-consent orchestrator. Owns the resolved `ConsentStatus`, raises `OnConsentChanged`, and exposes `IsConsentGranted`. Ads and analytics subscribe so nothing personalized runs until consent is granted.

- **`ConsentService`** — singleton `MonoBehaviour` (`IService`) delegating to a pluggable `IConsentProvider`.
- **Providers:** `UmpConsentProvider` / `UmpConsentProviderSO` (Google UMP GDPR/CCPA form, needs the Google Mobile Ads SDK) and `ManualConsentProvider` / `ManualConsentProviderSO` (built-in dialog, `ConsentDialog` / `ConsentDialogSettings`).
- **Core:** `ConsentRegion`, `ConsentStatus`, `ConsentStorage`.

### Loading System
`Runtime/LoadingSystem/`

Scene-flow and loading-screen manager.

- **`LoadingManager`** — singleton `MonoBehaviour` with static `OnLoadBegin` / `OnLoadProgress` / `OnLoadComplete` events; drives loads from a `SceneCatalog`.
- **Operations:** `ILoadingOperation`, `SceneTransitionOperation`, `ActionLoadingOperation`.
- **Data:** `SceneCatalog`, `SceneDefinition`, `LevelDefinition`.
- **UI:** `LoadingScreen`, `ScreenFader`.

### Global Event System
`Runtime/GlobalEventsSystem/GlobalEventSystem.cs`

A static, type-based publish/subscribe bus. `Listen<T>`, `Unlisten<T>`, and `Raise<T>` decouple senders from receivers; handlers are copied before dispatch to allow safe (un)subscription during a raise.

---

## Editor Tools

### Folder Generation Service
`Editor/FolderGenerationService.cs`

Generates a project folder structure from a list of relative paths, optionally dropping a `.gitkeep` in each folder. Written as an SRP-focused service returning a `GenerationReport`.

### SO Reset System
`Editor/SOResetSystem/`

Resets tracked ScriptableObjects when Play Mode stops (avoids leaking play-mode state into serialized assets).

- **`SOResetManagerWindow`** — `Tools ▸ SO Reset Manager` window with a drag-and-drop area to register objects.
- **`SOResetService`** — `[InitializeOnLoad]` service that calls `OnDisable()` on tracked objects when returning to Edit Mode.
- **`SOResetSettings`** — the persisted asset holding the tracked list.

### Multiple Sprite Project Icons
`Editor/MultipleSpriteProjectIcons.cs`

`[InitializeOnLoad]` tweak that draws each individual sprite's own sliced preview over the generic icon in the Project window's **list (one-column)** layout — including the sub-sprites of a "Multiple" sprite-mode texture. Toggle via `Tools ▸ Project Icons ▸ Preview Individual Sprites`.

### iOS Post-Process Build
`Editor/IosPostProcessBuild.cs`

`[PostProcessBuild]` step (iOS only) that edits the built Xcode project's `Info.plist` — adding the User Tracking usage description (when AdMob is installed) and SKAdNetwork identifiers (when Meta Ads is installed).

---

## Scripting Define Symbols

Several providers compile against third-party SDKs that may or may not be present. The relevant code paths are gated behind scripting define symbols so the package compiles cleanly even when an SDK is absent. Add the matching symbol under **Project Settings ▸ Player ▸ Scripting Define Symbols** only after importing the corresponding SDK.

| Define Symbol | Enables | Required SDK / Package |
| --- | --- | --- |
| `AdMob_SDK_INSTALLED` | AdMob ad provider, UMP consent provider, iOS User Tracking plist entry | Google Mobile Ads SDK |
| `MetaAds_SDK_INSTALLED` | SKAdNetwork identifiers in the iOS `Info.plist` | Meta Audience Network SDK |
| `AppLovin_Adapter_INSTALLED` | AppLovin mediation adapter in `AdMobProvider` | Google Mobile Ads AppLovin adapter |
| `Liftoff_Adapter_INSTALLED` | Liftoff/Vungle mediation adapter in `AdMobProvider` | Google Mobile Ads Liftoff/Vungle adapter |
| `UNITY_ADS_Adapter_INSTALLED` | Unity Ads mediation adapter in `AdMobProvider` | Google Mobile Ads Unity Ads adapter |
| `FIREBASE_SDK_INSTALLED` | Firebase analytics provider | Firebase Analytics SDK |
| `FIREBASE_AUTH_SDK_INSTALLED` | Firebase auth provider | Firebase Authentication SDK |

### Built-in Unity defines used

These are provided by Unity itself and require no manual setup:

| Define Symbol | Used for |
| --- | --- |
| `UNITY_EDITOR` | Editor-only guards (e.g. `AnalyticsService`, `SceneDefinition`, `LevelDefinition`). |
| `UNITY_IOS` | iOS post-process build (`IosPostProcessBuild`). |

> **Note:** Without a define set, its guarded provider still exists but its SDK calls are stubbed/no-op, so you can build and iterate before wiring up the real SDK.

---

## License

See the repository for license details.
