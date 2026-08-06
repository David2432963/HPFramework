# _Base Package (Maximum VContainer Edition) - Tài Liệu Hướng Dẫn & Kiến Trúc

`Assets/_Base` là một package nội bộ (internal package) chứa các mã nguồn, cấu trúc và tài nguyên có thể tái sử dụng độc lập giữa nhiều dự án game Unity khác nhau. Package này **đã bao gồm sẵn mã nguồn VContainer, UniTask và hỗ trợ Addressables**, sẵn sàng sử dụng ngay khi copy vào bất kỳ dự án Unity nào mà không cần cài đặt thêm UPM package nào khác.

Package được đóng gói và giới hạn bởi Assembly Definition `_Base.asmdef`.

---

## 1. Kiến Trúc VContainer & UniTask Cốt Lõi

* **Đã nhúng sẵn VContainer Framework:** Plugin VContainer (`Assets/_Base/Plugins/VContainer`) nhúng trực tiếp trong `_Base`.
* **Đã nhúng sẵn UniTask Framework:** Plugin UniTask (`Assets/_Base/Plugins/UniTask`) nhúng trực tiếp trong `_Base`.
* **Tích hợp UniTask & Addressables Asset Provider:**
  * Interface `IAssetProvider` bọc toàn bộ thao tác async load/instantiate asset:
    ```csharp
    UniTask<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken = default);
    UniTask<GameObject> InstantiateAsync(string key, Transform parent = null, CancellationToken cancellationToken = default);
    ```
  * `AddressablesAssetProvider`: Tự động sử dụng Addressables API khi dự án có cài đặt package `com.unity.addressables`, và tự động fallback về `Resources` / `Direct Instantiate` khi chưa cài Addressables.
* **Interface Abstractions (Phụ thuộc vào Interface):** Mọi service chính đều khai báo Interface và được đăng ký vào VContainer:
  * `IAssetProvider` (UniTask & Addressables Asset Management)
  * `ISettingsProvider` (Read-only cài đặt)
  * `IAudioService` (Quản lý BGM & SFX)
  * `IUIService` (Quản lý Screens, Popups, Toasts)
  * `IPoolService` (GameObject & Component Pooling)
  * `IHapticService` (Rung & Phản hồi lực)
  * `IProcedureSceneLoader` (Tải cảnh bất đồng bộ)
* **VContainer Native Atomic Instantiation:** `UIManager` và `PoolManager` sử dụng `objectResolver.Instantiate(prefab, parent)` native của VContainer — vừa Instantiate vừa Inject dependencies chỉ trong 1 bước duy nhất.
* **Injectable Pure C# Procedures:** Các trạng thái game (`Procedure`) hỗ trợ **Constructor Injection**. `ProcedureManager` sử dụng `IObjectResolver.Resolve()` để khởi tạo các state Procedure với đầy đủ phụ thuộc.
* **Scene Scoping cho Loading Scene:** Scene loading riêng (`LoadingScene`) có `LoadingLifetimeScope` kế thừa tự động từ `RootLifetimeScope`, inject `GameSceneManager` vào `LoadingScreen` mà không cần `FindObjectOfType` hay drag reference thủ công.
* **Global EntryPoint Exception Handling:** Tự động bắt và ghi log các ngoại lệ không được xử lý xảy ra trong VContainer EntryPoints.

---

## 2. Tổ Chức Thư Mục & Các Module Chính

### 📁 Assets/_Base/

* **`BaseConstants.cs`**: Quản lý tập trung tất cả hằng số nội bộ của `_Base` (PlayerPrefs keys, Scene names, Audio keys...).
* **`BaseLog.cs`**: Log wrapper với `[Conditional("ENABLE_BASE_LOG")]` giúp zero-allocation trên Production build.

* **`Plugins/`**: Mã nguồn các Framework được nhúng trực tiếp:
  * `VContainer/`: VContainer DI Framework (Runtime & Editor Diagnostics Window).
  * `UniTask/`: UniTask Async/Await Framework (Runtime & Editor Task Tracker).
  * `Sirenix/`: Odin Inspector Framework (Rich Editor & Buttons).

* **`Scripts/Assets/` (Quản lý Asset Async)**
  * [IAssetProvider.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Assets/IAssetProvider.cs): Interface async asset loading bằng UniTask.
  * [AddressablesAssetProvider.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Assets/AddressablesAssetProvider.cs): Provider hỗ trợ UniTask + Addressables với Resources fallback.

* **`Scripts/Bootstrap/` (Luồng khởi tạo & State Machine)**
  * [RootLifetimeScope.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Bootstrap/RootLifetimeScope.cs): Composition Root chính của ứng dụng.
  * [GameSceneManager.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Bootstrap/GameSceneManager.cs): Quản lý chuyển cảnh bất đồng bộ với LoadingScene Additive overlay.
  * [Procedure.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Bootstrap/Procedure.cs): Base class cho các trạng thái vòng đời game.
  * [ProcedureManager.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Bootstrap/ProcedureManager.cs): FSM điều phối các `Procedure` được resolve từ VContainer.

* **`Scripts/Audio/` (Âm thanh)**
  * [IAudioService.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Audio/IAudioService.cs): Interface cho hệ thống audio.
  * [AudioManager.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Audio/AudioManager.cs): Service điều khiển BGM & SFX channels pool.

* **`Scripts/UI/` (Giao diện)**
  * [IUIService.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/UI/IUIService.cs): Interface cho hệ thống UI.
  * [UIManager.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/UI/UIManager.cs): Quản lý nạp, hiển thị và đóng Screens/Popups.
  * [LoadingLifetimeScope.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/UI/LoadingLifetimeScope.cs) & [LoadingScreen.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/UI/LoadingScreen.cs): Component hiển thị tiến trình nạp game trong `LoadingScene`.

* **`Scripts/Persistence/` (Cài đặt & Lưu trữ)**
  * [ISettingsProvider.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Persistence/ISettingsProvider.cs): Interface truy cập cài đặt read-only.
  * [SettingsManager.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Persistence/SettingsManager.cs): Pure C# service lưu cache cài đặt trong RAM, tự động ghi PlayerPrefs khi dispose.

* **`Scripts/Pooling/` (Tối ưu bộ nhớ)**
  * [IPoolService.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Pooling/IPoolService.cs): Interface cho object pooling.
  * [PoolManager.cs](file:///d:/Repo_Unity/Projects/BaseVContainer/Assets/_Base/Scripts/Pooling/PoolManager.cs): Bể chứa GameObject/Component với VContainer atomic instantiation.

---

## 3. Hướng Dẫn Tích Hợp Vào Dự Án Mới

1. Copy toàn bộ thư mục `Assets/_Base` vào dự án Unity của bạn.
2. Tạo GameObject trong Scene đầu tiên, gắn script `RootLifetimeScope`. Bấm nút **`⚡ Auto Setup Hierarchy & References`** để tự động tạo và link Canvases/Camera trong 1 click.
3. Khai báo Constructor Injection trong các class `Procedure` state:
   ```csharp
   public class GameplayProcedure : Procedure
   {
       private readonly IUIService uiService;
       private readonly IAudioService audioService;
       private readonly IAssetProvider assetProvider;

       public GameplayProcedure(IUIService uiService, IAudioService audioService, IAssetProvider assetProvider)
       {
           this.uiService = uiService;
           this.audioService = audioService;
           this.assetProvider = assetProvider;
       }

       public override void OnEnter()
       {
           uiService.ShowScreen<GameplayScreen>();
           audioService.PlayMusic("gameplay_bgm");
       }
   }
   ```
4. Đăng ký các Procedure vào `RootLifetimeScope`:
   ```csharp
   builder.Register<GameplayProcedure>(Lifetime.Transient);
   ```
