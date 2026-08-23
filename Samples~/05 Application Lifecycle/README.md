# Application Lifecycle

Register `SampleLifecycleObserver` as a singleton entry point from your project `RootLifetimeScope`. It reacts to low-memory and quit signals without making the low-level Lifecycle module depend on assets, pools, or persistence.
