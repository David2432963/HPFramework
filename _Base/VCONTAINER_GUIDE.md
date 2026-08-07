# VContainer Guide cho Base Framework

Tài liệu này mô tả cách dùng VContainer trong `com.base.vcontainer` sau đợt ổn định kiến trúc `1.1.0`.

## 1. Một composition root duy nhất

`RootLifetimeScope` là nơi đăng ký service global. Mọi dependency quan trọng phải nhìn thấy được tại đây hoặc trong child scope tương ứng.

```text
RootLifetimeScope
├── ISettingsService
├── IAssetProvider
├── IAudioService
├── IUIService
├── IPoolService
├── IHapticService
├── IProcedureService
├── IProcedureSceneLoader
└── IIAPProvider
```

Không tạo thêm static singleton để truy cập các service này.

## 2. Đăng ký service theo đúng lifetime

- `Singleton`: sống cùng root scope, ví dụ settings, procedure manager, asset provider.
- `Scoped`: sống cùng scene/feature scope.
- `Transient`: object stateless, nhẹ và được tạo theo nhu cầu.

```csharp
protected override void Configure(IContainerBuilder builder)
{
    builder.Register<ScoreService>(Lifetime.Scoped)
        .As<IScoreService>();

    builder.RegisterEntryPoint<GameplayLoop>();
}
```

Đừng đăng ký một service stateful dạng transient nếu nhiều consumer cần dùng chung state.

## 3. EntryPoints là lifecycle chính

Framework dùng VContainer EntryPoints làm lifecycle cho service:

| Interface | Thời điểm |
|---|---|
| `IInitializable` | Sau khi container build |
| `IStartable` | Bắt đầu scope |
| `IAsyncStartable` | Khởi tạo async |
| `ITickable` | Mỗi Update |
| `IFixedTickable` | Mỗi FixedUpdate |
| `ILateTickable` | Mỗi LateUpdate |
| `IDisposable` | Scope bị dispose |

Ví dụ:

```csharp
public sealed class GameplayLoop :
    IStartable,
    ITickable,
    IDisposable
{
    private readonly IInputReader input;

    public GameplayLoop(IInputReader input)
    {
        this.input = input;
    }

    public void Start()
    {
        input.EnableGameplay();
    }

    public void Tick()
    {
        // Gameplay update.
    }

    public void Dispose()
    {
        input.DisableGameplay();
    }
}
```

Đăng ký bằng:

```csharp
builder.RegisterEntryPoint<GameplayLoop>();
```

Không tạo một Update dispatcher thứ hai và không scan toàn bộ MonoBehaviour bằng reflection.

## 4. MonoBehaviour vẫn có vai trò rõ ràng

Giữ MonoBehaviour khi object cần:

- Transform/hierarchy.
- Inspector serialization.
- Unity callbacks đặc thù.
- Renderer, AudioSource, Camera hoặc UI component.

Inject qua method:

```csharp
public sealed class RewardPopup : MonoBehaviour
{
    private IAudioService audioService;

    [Inject]
    public void Construct(IAudioService audioService)
    {
        this.audioService = audioService;
    }
}
```

Prefab phải được tạo qua `IObjectResolver.Instantiate` hoặc system đã dùng resolver như `PoolManager`/`UIManager` để injection chạy.

## 5. Procedure đăng ký explicit

```csharp
public sealed class GameLifetimeScope : RootLifetimeScope
{
    protected override void RegisterProcedures(IContainerBuilder builder)
    {
        builder.Register<LaunchProcedure>(Lifetime.Singleton)
            .As<Procedure>()
            .AsSelf();

        builder.Register<GameplayProcedure>(Lifetime.Singleton)
            .As<Procedure>()
            .AsSelf();
    }
}
```

`ProcedureManager` không quét assembly và không tự instantiate type chưa đăng ký. Điều này giúp:

- Startup deterministic.
- IL2CPP stripping dễ kiểm soát.
- Constructor injection hoạt động rõ ràng.
- Test procedure không cần Unity scene.

```csharp
await procedureService.ChangeStateAsync<GameplayProcedure>(token);
```

Nếu `OnEnterAsync` thất bại, manager giữ lại procedure trước và đặt transition state thành `Failed`.

## 6. Scene scope

Service chỉ dùng trong một scene nên đặt trong child `LifetimeScope` của scene:

```csharp
public sealed class GameplayLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<EnemyRegistry>(Lifetime.Scoped);
        builder.Register<CombatService>(Lifetime.Scoped);
        builder.RegisterEntryPoint<GameplayPresenter>();
    }
}
```

Khi scene/scope bị hủy, VContainer dispose các service scoped. Mọi service subscribe event hoặc giữ native resource phải implement `IDisposable`.

## 7. Không dùng Resolve như service locator

Ưu tiên constructor/method injection. `Resolve<T>()` chỉ nên xuất hiện tại infrastructure boundary, test hoặc nơi thật sự cần dynamic lookup.

Không làm:

```csharp
void Update()
{
    LifetimeScope.Find<RootLifetimeScope>()
        .Container.Resolve<IPlayerService>()
        .Tick();
}
```

Nên làm:

```csharp
public PlayerPresenter(IPlayerService playerService)
{
    this.playerService = playerService;
}
```

## 8. Pool và UI injection

`PoolManager` dùng resolver khi tạo instance mới, sau đó gọi:

- `IPoolable.OnSpawn()` khi lấy object.
- `IPoolable.OnDespawn()` khi trả object.

Không inject lại mỗi lần spawn. Injection là thiết lập dependency; `OnSpawn` là reset runtime state.

UI catalog lazy-create screen/popup. View phải hủy coroutine, event và animation trong hide/despawn/destroy.

## 9. Optional integrations

Core không reference cứng:

- DOTween.
- Odin Inspector.
- URP.
- Addressables.

Mỗi integration thật nên nằm trong asmdef riêng và implement interface core. Ví dụ Addressables:

```csharp
public sealed class GameLifetimeScope : RootLifetimeScope
{
    protected override void RegisterAssetProvider(IContainerBuilder builder)
    {
        builder.Register<AddressablesProvider>(Lifetime.Singleton)
            .As<IAssetProvider>()
            .As<IDisposable>();
    }
}
```

## 10. Checklist trước khi merge

1. Package import vào host project sạch không có compiler error.
2. `Tools/Validate-Package.ps1` pass.
3. EditMode và PlayMode tests pass.
4. Prefab không có Missing Script.
5. Không thêm service locator/static singleton mới.
6. Async API có cancellation và error path.
7. Event subscription được tháo trong `Dispose`/`OnDestroy`.
8. Integration tùy chọn không được thêm vào core asmdef.
