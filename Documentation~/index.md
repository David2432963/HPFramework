# HP Framework Documentation

HP Framework is organized around explicit VContainer ownership: application lifetime at the root, scene/menu/gameplay lifetime below it, and shorter feature lifetimes below scenes.

During active development the framework is consumed as editable source under `Assets/Plugins/HPFramework`. Setup creates host-project Bootstrap/settings beside that source while reusable templates, runtime code, tests and documentation stay versioned in the framework repository.

The documentation reflects the current domain-owned Bootstrap hierarchy, non-destructive Repair vs explicit Reset behavior, optional Input System integration in editable Assets mode, URP Base/Overlay UI camera stacking, scoped UI/pooling/EventBus architecture, and the framework's runtime performance contracts.

## Guides

- [Installation](installation.md)
- [Getting started](getting-started.md)
- [Architecture](architecture.md)
- [Lifetime scopes](scopes.md)
- [UI and pooling](ui-and-pooling.md)
- [Runtime performance](runtime-performance.md)
- [Troubleshooting](troubleshooting.md)
- [Migration to 3.0](migration-3.0.md)

For a fast orientation, start with `README.md`, then read **Getting started** and **Architecture** before adding game-specific services.
