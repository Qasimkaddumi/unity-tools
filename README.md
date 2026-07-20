# Unity Tools

A modular set of drop-in tools and services for any Unity project.

- **Package:** `com.kaddumi.unity-tools`
- **Version:** `0.1.0`
- **Unity:** `6000.3` or newer
- **Dependencies:** `com.unity.ugui` (2.0.0)
- **Author:** [Qasim Kaddumi](https://qasimkaddumi.github.io/portfolio/)

Two assemblies: `KaddumiUnityTools.Runtime` (`Runtime/`) for gameplay services, and `KaddumiUnityTools.Editor` (`Editor/`) for editor-only tooling.

## Installation

Add via Package Manager (`Add package from git URL...`) or reference it in `Packages/manifest.json`. Both assemblies are picked up automatically.

Most runtime systems follow the same pattern: a singleton **Manager** (`IService`, resolved via `ServiceLocator`) delegates to an SDK-agnostic **provider** — a `ScriptableObject` asset you assign in the inspector, created via **Assets ▸ Create ▸ Kaddumi ▸ …**. This keeps game code SDK-agnostic and lets you swap backends without touching gameplay code.

## Runtime Systems

| System | Location | What it does |
| --- | --- | --- |
| Service Locator | `Serivces_System/` | Resolves `IService` components; the backbone the other managers register with. |
| Ads | `Ads_System/` | `AdManager` + providers for AdMob, LevelPlay/ironSource, AppLovin MAX, Unity Ads. |
| Analytics | `Analytics_System/` | `AnalyticsManager` fans events out to Firebase, Unity Analytics, GameAnalytics, Amplitude, Mixpanel, or a debug logger. |
| Authentication | `Authentication_System/` | `AuthManager` routes sign-in to Firebase, Unity Gaming Services, or PlayFab. |
| Consent | `ConsentService/` | `ConsentService` gates personalized ads/analytics behind GDPR/CCPA consent (Google UMP or a built-in manual dialog). |
| Loading | `LoadingSystem/` | `LoadingManager` drives scene transitions and loading-screen UI from a `SceneCatalog`. |
| Audio | `Audio_System/` | `AudioManager` — pooled SFX, crossfaded music, mixer buses with ducking; plays `SoundDefinition` assets by reference or string ID. |
| Save | `Save_System/` | `SaveManager` persists `ISaveable` objects to PlayerPrefs, JSON file, encrypted file, or a remote API. |
| Api | `Api_System/` | `ApiManager`/`ApiClient` — async HTTP client with auth-token handling, no coroutine host needed. |
| Game Statistics | `Game_Statistics_System/` | `StatisticsManager` tracks per-player metrics (counters, FPS, per-run data) with debounced JSON persistence. |
| Global Events | `GlobalEventsSystem/` | Static type-based pub/sub bus (`Listen<T>`/`Unlisten<T>`/`Raise<T>`). |

## Editor Tools

Enable/disable any of these from **Tools ▸ Unity Tools ▸ Tool Manager** (choices persist via `EditorPrefs`).

| Tool | Location | What it does |
| --- | --- | --- |
| Folder Generation | `Editor/FolderGenerationService.cs` | Generates a project folder structure from a list of paths. |
| SO Reset System | `Editor/SOResetSystem/` | Resets tracked ScriptableObjects when Play Mode stops. |
| Multi-Sprite Icon Preview | `Editor/MultipleSpriteProjectIcons.cs` | Shows per-sprite thumbnails (incl. sub-sprites) in the Project window's list view, with hover zoom. |
| iOS Post-Process Build | `Editor/IosPostProcessBuild.cs` | Adds User Tracking usage description and SKAdNetwork IDs to the built Xcode `Info.plist`. |

## Scripting Define Symbols

Third-party providers compile against optional SDKs. Add the matching define under **Project Settings ▸ Player ▸ Scripting Define Symbols** once you've imported that SDK — without it, the provider still compiles but its calls are stubbed/no-op.

| Define | SDK |
| --- | --- |
| `AdMob_SDK_INSTALLED` | Google Mobile Ads (also enables UMP consent, iOS tracking plist entry) |
| `MetaAds_SDK_INSTALLED` | Meta Audience Network (SKAdNetwork plist entries) |
| `AppLovin_Adapter_INSTALLED` | AppLovin mediation adapter |
| `Liftoff_Adapter_INSTALLED` | Liftoff/Vungle mediation adapter |
| `UNITY_ADS_Adapter_INSTALLED` | Unity Ads mediation adapter |
| `LevelPlay_SDK_INSTALLED` | Unity LevelPlay / ironSource (`com.unity.services.levelplay`) |
| `AppLovinMAX_SDK_INSTALLED` | AppLovin MAX Unity plugin |
| `UnityAds_SDK_INSTALLED` | Unity Ads (`com.unity.ads`) |
| `FIREBASE_SDK_INSTALLED` | Firebase Analytics |
| `UNITY_ANALYTICS_SDK_INSTALLED` | Unity Gaming Services Analytics |
| `GAMEANALYTICS_SDK_INSTALLED` | GameAnalytics |
| `AMPLITUDE_SDK_INSTALLED` | Amplitude |
| `MIXPANEL_SDK_INSTALLED` | Mixpanel (`com.mixpanel.unity`) |
| `FIREBASE_AUTH_SDK_INSTALLED` | Firebase Authentication |
| `UNITY_AUTHENTICATION_SDK_INSTALLED` | Unity Gaming Services Authentication |
| `PLAYFAB_SDK_INSTALLED` | PlayFab |

## License

See the repository for license details.
