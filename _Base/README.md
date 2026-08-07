# Base VContainer Framework

`com.base.vcontainer` là Unity package dùng làm nền tảng chung cho game: dependency injection, procedure state machine, scene flow, UI, audio, pooling, input, persistence, IAP abstraction và diagnostics.

Bản `1.1.0` tập trung vào ba yêu cầu:

- Import package vào project sạch phải compile được.
- Bootstrap phải build container theo thứ tự xác định và không có Missing Script.
- Runtime core không phụ thuộc cứng vào DOTween, Odin, URP hoặc Addressables.

## Yêu cầu

- Unity `2021.3` trở lên.
- Các dependency UPM được khai báo trong `package.json`:
  - Input System
  - Newtonsoft Json
  - TextMeshPro
  - UGUI
- VContainer và UniTask đã được nhúng trong package.

## Cài đặt

Thêm package từ Git URL hoặc local path trong Package Manager. Với local package:

```json
{
  "dependencies": {
    "com.base.vcontainer": "file:../BaseVContainer/_Base"
  }
}
```

Không copy riêng file `.cs`. Phải giữ nguyên toàn bộ package và các file `.meta` để GUID prefab/script không bị thay đổi.

## Khởi tạo Bootstrap

Có hai cách:

1. Kéo `Prefabs/Bootstrap.prefab` vào scene đầu tiên.
2. Dùng menu `Base > Create Bootstrap Prefab` để Unity tạo lại prefab hợp lệ trong `Assets/_Base/Prefabs`.

Bootstrap mặc định chứa:

- `RootLifetimeScope`
- `AudioManager`
- `UIManager`
- `InputManager`
- `GameSceneManager`
- `PoolManager`
- `HapticManager`
- UI camera/canvas roots
- Pool root

Các catalog như `AudioLibrarySO`, `UICatalogSO`, input asset và toast prefab có thể được gán sau trong Inspector.

## Composition Root

Tạo scope của game bằng cách kế thừa `RootLifetimeScope` và đăng ký procedure một cách explicit:

```csharp
using VContainer;
using Base.Bootstrap;

public sealed class GameLifetimeScope : RootLifetimeScope
{
    protected override void RegisterProcedures(IContainerBuilder builder)
    {
        builder.Register<LaunchProcedure>(Lifetime.Singleton)
            .As<Procedure>()
            .AsSelf();

        builder.Register<MenuProcedure>(Lifetime.Singleton)
            .As<Procedure>()
            .AsSelf();
    }
}
```

`ProcedureManager` nhận `IEnumerable<Procedure>` từ VContainer. Framework không quét assembly, không dùng `Activator.CreateInstance()` và không tự tạo procedure chưa đăng ký.

## Procedure

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Base.UI;

public sealed class MenuProcedure : Procedure
{
    private readonly IUIService uiService;

    public MenuProcedure(IUIService uiService)
    {
        this.uiService = uiService;
    }

    public override UniTask OnEnterAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        uiService.ShowScreen<MenuScreen>();
        return UniTask.CompletedTask;
    }
}
```

Chuyển state:

```csharp
await procedureService.ChangeStateAsync<MenuProcedure>(cancellationToken);
```

Mỗi transition có trạng thái `Idle`, `Exiting`, `Entering` hoặc `Failed`. Transition async được serialize để không có hai transition chạy đồng thời.

## Scene Flow

`GameSceneManager` sử dụng `UniTask` làm API chính:

```csharp
Scene scene = await sceneManager.LoadSceneAsync(
    "Gameplay",
    fakeLoadingDuration: 0.5f,
    cancellationToken);
```

Contract của một request load:

- Hoàn tất và trả về `Scene` hợp lệ.
- Ném exception có nội dung rõ ràng.
- Hoặc bị hủy bằng `OperationCanceledException`.

Không còn callback completion source có thể treo vô hạn. Framework cũng không tự gọi `Resources.UnloadUnusedAssets()` sau mỗi lần chuyển scene.

## Asset Provider

Core đăng ký `ResourcesAssetProvider` mặc định:

```csharp
GameObject view = await assetProvider.InstantiateAsync(
    "UI/MyView",
    parent,
    cancellationToken);

assetProvider.ReleaseInstance(view);
```

`AddressablesAssetProvider` trong core chỉ là alias tương thích cũ và hiện delegate sang Resources. Dự án cần Addressables nên đặt implementation thật trong một integration assembly riêng rồi override:

```csharp
protected override void RegisterAssetProvider(IContainerBuilder builder)
{
    builder.Register<MyAddressablesAssetProvider>(Lifetime.Singleton)
        .As<IAssetProvider>();
}
```

## Settings

`ISettingsService` là nguồn dữ liệu mutable duy nhất cho audio, haptic, quality và frame rate. Không nên để từng manager tự đọc/ghi PlayerPrefs riêng.

```csharp
settingsService.MusicEnabled = false;
settingsService.MusicVolume = 0.6f;
settingsService.Save();
```

## Pooling

Prefab có thể implement `IPoolable`:

```csharp
public sealed class Projectile : MonoBehaviour, IPoolable
{
    public void OnSpawn()
    {
        // Reset state cho lượt sử dụng mới.
    }

    public void OnDespawn()
    {
        // Hủy timer, event và hiệu ứng đang chạy.
    }
}
```

Pool có duplicate-release protection và giới hạn số instance inactive cho mỗi prefab.

## UI

UI catalog được load lazy. Framework không preload toàn bộ prefab khi bootstrap. Các transition built-in dùng coroutine + unscaled time và `AnimationCurve`, không yêu cầu DOTween.

URP camera stacking được phát hiện bằng reflection khi URP tồn tại; project không có URP vẫn compile.

## IAP

- Editor/Development Build: `SimulatedIAPProvider`.
- Release Build: `UnavailableIAPProvider` cho tới khi game đăng ký provider production thật.

Không phát hành game với simulated provider.

## Kiểm tra package

Static integrity check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Validate-Package.ps1
```

Kiểm tra này phát hiện:

- Asset/folder thiếu `.meta`.
- GUID trùng hoặc sai format.
- asmdef/package JSON lỗi.
- asmdef reference không resolve.
- prefab chứa script GUID hỏng.
- lifecycle/service-locator cũ quay trở lại.
- dependency cứng DOTween/Odin hoặc global unload.

Test assemblies có trong package:

- `Base.Tests.Editor`: procedure, utility và prefab integrity.
- `Base.Tests.PlayMode`: dựng Bootstrap thật, build VContainer container và resolve service lõi.

Kết quả validation hiện tại trên Unity `6000.0.77f1`:

- Package import/compile: pass.
- EditMode: `7/7` pass.
- PlayMode: `1/1` pass.

## Module chính

```text
_Base/
├── Scripts/
│   ├── Assets
│   ├── Audio
│   ├── Bootstrap
│   ├── Common
│   ├── IAP
│   ├── Input
│   ├── Persistence
│   ├── Pooling
│   └── UI
├── Graphics
├── Diagnostics
├── Plugins
│   ├── VContainer
│   └── UniTask
├── Prefabs
└── Tests
```

## Nguyên tắc mở rộng

- Dependency được đăng ký tại composition root.
- Dùng VContainer EntryPoints cho lifecycle service.
- Không thêm global singleton/service locator mới.
- Integration tùy chọn phải có asmdef riêng.
- Mọi async operation phải hoàn tất, throw hoặc cancel.
- Mọi pooled object phải reset state khi spawn/despawn.
