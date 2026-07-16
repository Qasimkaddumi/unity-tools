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
| `KaddumiUnityTools.Runtime` | `Runtime/` | Gameplay/runtime services (ads, analytics, auth, consent, save, loading, events, service locator). |
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
- **Providers:**
  - `AdMobProvider` / `AdMobProviderSO` (Google Mobile Ads, with AppLovin, Liftoff/Vungle and Unity Ads mediation adapters).
  - `LevelPlayProvider` / `LevelPlayProviderSO` (Unity LevelPlay / ironSource mediation, SDK 8.x Init API).
  - `AppLovinMaxProvider` / `AppLovinMaxProviderSO` (standalone AppLovin MAX mediation).
  - `UnityAdsProvider` / `UnityAdsProviderSO` (Unity Ads / `Advertisement` SDK used directly).

  Each provider is a `ScriptableObject` factory — create the matching asset via **Assets ▸ Create ▸ Kaddumi ▸ Ads ▸ Providers** and assign it to `AdManager`. Set the provider's app/SDK key and any Game IDs in the corresponding class, and add the SDK's define symbol (below).
- **Core:** `AdType`, `AdReward`, `AdErrorDomain`; config via `AdConfig`.

### Analytics System
`Runtime/Analytics_System/`

Multi-backend analytics — all assigned providers receive every event.

- **`AnalyticsManager`** — singleton `MonoBehaviour` (`IService`) owning an `AnalyticsService`; accepts a list of `AnalyticsProviderSO` assets.
- **Providers:** `FirebaseAnalyticsProvider`, `UnityAnalyticsProvider`, `GameAnalyticsProvider`, `AmplitudeAnalyticsProvider`, `MixpanelAnalyticsProvider` (each with a matching `…SO` asset), and `DebugLoggerProvider` / `DebugLoggerProviderSO` for editor/testing.

### Authentication System
`Runtime/Authentication_System/`

SDK-agnostic sign-in, structurally mirroring the analytics manager. Each request is routed to a provider that supports the requested `AuthMethod`.

- **`AuthManager`** — singleton `MonoBehaviour` (`IService`) owning an `AuthService`; exposes `CurrentUser`, `IsSignedIn`, and `OnSignedIn` / `OnSignedOut` events. Optional shared `AuthConfig`.
- **Providers:** `FirebaseAuthProvider`, `UnityAuthProvider` (Unity Gaming Services), `PlayFabAuthProvider` (each with a matching `…SO` asset), and `DebugAuthProvider` / `DebugAuthProviderSO` for editor/testing.
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

### Save System
`Runtime/Save_System/`

SDK-agnostic save/load, structurally mirroring the other managers. Game objects implement `ISaveable` (or derive from `SaveableBehaviour<TState>`), register themselves, and the system captures each one into a versioned, slot-based save routed through a pluggable storage backend.

- **`SaveManager`** — singleton `MonoBehaviour` (`IService`) owning a `SaveService`; exposes `Save`/`Load`/`Delete`/`HasSave`/`GetMetadata`, an `ActiveSlot`, and `OnSaved` / `OnLoaded` / `OnError` events. Drives auto-save, save-on-pause/quit, and play-time tracking from `SaveConfig`.
- **`SaveService`** — plain-C# domain service that owns the `ISaveable` registry and orchestrates capture → serialize → write and read → deserialize → restore.
- **Providers:** `PlayerPrefsSaveProvider` (every platform, incl. WebGL), `FileSaveProvider` (JSON files under `persistentDataPath`, atomic writes), and `EncryptedFileSaveProvider` (AES-256/PBKDF2) — each with a matching `…SO` asset created via **Assets ▸ Create ▸ Kaddumi ▸ Save ▸ Providers**.
- **Serialization:** `ISaveSerializer` / `JsonSaveSerializer` (Unity `JsonUtility`, zero dependencies).
- **Core:** `SaveData`, `SaveMetadata`, `SaveResult`, `SaveError`; config via `SaveConfig`.

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
| `LevelPlay_SDK_INSTALLED` | Unity LevelPlay / ironSource ad provider (`LevelPlayProvider`) | Unity LevelPlay SDK (`com.unity.services.levelplay`, 8.x) |
| `AppLovinMAX_SDK_INSTALLED` | Standalone AppLovin MAX ad provider (`AppLovinMaxProvider`) | AppLovin MAX Unity plugin |
| `UnityAds_SDK_INSTALLED` | Direct Unity Ads provider (`UnityAdsProvider`) | Unity Advertisement SDK (`com.unity.ads`) |
| `FIREBASE_SDK_INSTALLED` | Firebase analytics provider | Firebase Analytics SDK |
| `UNITY_ANALYTICS_SDK_INSTALLED` | Unity Analytics provider | Unity Gaming Services Analytics (`com.unity.services.analytics`) |
| `GAMEANALYTICS_SDK_INSTALLED` | GameAnalytics provider | GameAnalytics Unity SDK |
| `AMPLITUDE_SDK_INSTALLED` | Amplitude analytics provider | Amplitude Unity plugin |
| `MIXPANEL_SDK_INSTALLED` | Mixpanel analytics provider | Mixpanel Unity SDK (`com.mixpanel.unity`) |
| `FIREBASE_AUTH_SDK_INSTALLED` | Firebase auth provider | Firebase Authentication SDK |
| `UNITY_AUTHENTICATION_SDK_INSTALLED` | Unity Authentication provider | Unity Gaming Services Authentication (`com.unity.services.authentication`) |
| `PLAYFAB_SDK_INSTALLED` | PlayFab auth provider | PlayFab Unity SDK |

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
