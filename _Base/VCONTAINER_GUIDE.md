# 🚀 Hướng Dẫn & Tối Ưu Hóa Sử Dụng VContainer Trong Base Framework

Tài liệu này tổng hợp toàn bộ nguyên lý thiết kế, quy chuẩn và hướng dẫn tối ưu hóa **VContainer** trong **Base Framework**.

---

## 📑 Mục Lục
1. [Triết Lý Kiến Trúc & Composition Root](#1-triết-lý-kiến-trúc--composition-root)
2. [Quản Lý Vòng Đời & Scoped Containers (Hierarchy Scopes)](#2-quản-lý-vòng-đời--scoped-containers-hierarchy-scopes)
3. [VContainer EntryPoints (Thay Thế MonoBehaviour)](#3-vcontainer-entrypoints-thay-thế-monobehaviour)
4. [Tối Ưu Procedure State Machine (Không Reflection)](#4-tối-ưu-procedure-state-machine-không-reflection)
5. [Auto-Injection Cho Object Pooling & UI](#5-auto-injection-cho-object-pooling--ui)
6. [Best Practices & Performance Tuning (IL2CPP)](#6-best-practices--performance-tuning-il2cpp)

---

## 1. Triết Lý Kiến Trúc & Composition Root

Base Framework áp dụng mô hình **Dependency Injection (DI)** chuẩn mực với VContainer:
- **Loose Coupling**: Các component chỉ phụ thuộc vào `Interface` thay vì concrete class.
- **Explicit Dependencies**: Ưu tiên **Constructor Injection** để minh bạch các dependency mà class cần.
- **Single Composition Root**: `RootLifetimeScope` đóng vai trò là nơi duy nhất đăng ký các dependency toàn cục.

```
[ RootLifetimeScope (Global) ]
       │
       ├──► UIManager (IUIService)
       ├──► AudioManager (IAudioService)
       ├──► SettingsManager (ISettingsProvider)
       ├──► PoolManager (IPoolService)
       └──► ProcedureManager (IProcedureService)
```

---

## 2. Quản Lý Vòng Đời & Scoped Containers (Hierarchy Scopes)

Để tránh memory leak và giữ cho bộ nhớ sạch sẽ, dự án chia Scope theo phân cấp:

### A. RootLifetimeScope (Global / DontDestroyOnLoad)
Chứa các service sống suốt vòng đời ứng dụng (Audio, UI System, Save/Load, IAP).

### B. Scene LifetimeScopes (Child Scope)
Mỗi Scene (ví dụ: `MainMenuLifetimeScope`, `GamePlayLifetimeScope`) sẽ kế thừa từ `RootLifetimeScope`.

```csharp
public class GamePlayLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Đăng ký các service CHỈ dùng trong trận đấu
        builder.Register<EnemySpawner>(Lifetime.Scoped);
        builder.Register<ScoreSystem>(Lifetime.Scoped);
        
        // Đăng ký EntryPoint cho Gameplay
        builder.RegisterEntryPoint<GameplayLoopSystem>();
    }
}
```
> 💡 **Lợi ích**: Khi Scene bị unload, VContainer sẽ **tự động Dispose toàn bộ service trong Child Scope** và giải phóng bộ nhớ 100%.

---

## 3. VContainer EntryPoints (Thay Thế MonoBehaviour)

Hạn chế viết logic trong `MonoBehaviour.Update()`. Thay vào đó, hãy sử dụng **Pure C# Class** kết hợp VContainer EntryPoints:

| Interface | Tương đương trong Unity | Công dụng |
| :--- | :--- | :--- |
| `IInitializable` | `Awake()` / `Start()` | Khởi tạo đồng bộ khi Scope tạo ra |
| `IAsyncStartable` | `async Task Start()` | Khởi tạo bất đồng bộ (Load Asset, Load Data) |
| `IStartable` | `Start()` | Chạy ngay sau khi khởi tạo xong |
| `ITickable` | `Update()` | Chạy mỗi frame (Cực nhẹ, ít CPU Overhead) |
| `IFixedTickable` | `FixedUpdate()` | Chạy theo chu kỳ vật lý |
| `IDisposable` | `OnDestroy()` | Tự động hủy event listener & giải phóng bộ nhớ |

### Ví dụ mẫu EntryPoint:
```csharp
public class GameLogicSystem : ITickable, IStartable, IDisposable
{
    private readonly InputManager _inputManager;
    private readonly IPoolService _poolService;

    // Constructor Injection
    public GameLogicSystem(InputManager inputManager, IPoolService poolService)
    {
        _inputManager = inputManager;
        _poolService = poolService;
    }

    public void Start()
    {
        // Setup logic khi game bắt đầu
    }

    public void Tick()
    {
        // Logic chạy theo frame (thay thế MonoBehaviour Update)
    }

    public void Dispose()
    {
        // Unsubscribe events tự động khi Scope bị hủy
    }
}
```

Đăng ký trong Scope:
```csharp
builder.RegisterEntryPoint<GameLogicSystem>();
```

---

## 4. Tối Ưu Procedure State Machine (Không Reflection)

Để tối ưu tốc độ khởi động game trên Mobile/WebGL, `ProcedureManager` không sử dụng Reflection để quét Assembly.

### Đăng ký Procedure trong `RootLifetimeScope`:
```csharp
// Đăng ký các Procedure vào VContainer
builder.Register<ProcedureLaunch>(Lifetime.Singleton).As<Procedure>();
builder.Register<ProcedureMenu>(Lifetime.Singleton).As<Procedure>();
builder.Register<ProcedureGameplay>(Lifetime.Singleton).As<Procedure>();

// Đăng ký ProcedureManager nhận tự động danh sách Procedure
builder.Register<ProcedureManager>(Lifetime.Singleton)
       .As<IProcedureService>();
```

---

## 5. Auto-Injection Cho Object Pooling & UI

Khi sinh ra GameObject từ Prefab, dùng `IObjectResolver.Instantiate()` để VContainer **tự động Inject** dependency vào tất cả MonoBehaviour gắn trên Prefab đó:

```csharp
// Trong UIManager hoặc PoolManager
public class PoolManager : MonoBehaviour, IPoolService
{
    private IObjectResolver _resolver;

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
    }

    private GameObject CreateInstance(GameObject prefab, Transform parent)
    {
        // Sinh ra Object và tiêm phụ thuộc (Dependency Injection) trong 1 bước
        return _resolver != null 
            ? _resolver.Instantiate(prefab, parent) 
            : Instantiate(prefab, parent);
    }
}
```

---

## 6. Best Practices & Performance Tuning (IL2CPP)

1. **Ưu tiên Constructor Injection**: Giúp VContainer hoạt động với hiệu năng cao nhất và dễ viết Unit Test.
2. **Bật VContainer Source Generator**: Trong Unity `Project Settings -> VContainer`, bật Code Generation để tránh Reflection hoàn toàn trên iOS/Android (IL2CPP).
3. **Tránh Service Locator (`objectResolver.Resolve<T>()`)**: ngoại trừ các Manager đặc thù như `ProcedureManager` hoặc `UIManager` khi cần Instantiate động. Còn lại luôn truyền Dependency qua Constructor.
