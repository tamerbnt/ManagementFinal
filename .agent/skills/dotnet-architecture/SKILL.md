---
name: dotnet-architecture
description: Best practices for C# .NET 8, MVVM, and Thread Safety. Use this for ViewModels and Services.
---

# .NET Architecture Standards

## 1. MVVM Implementation
- **Library:** Use `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).
- **Async:** Never use `async void`. Always use `async Task`.
- **Initialization:** Do NOT put Database/API calls in the Constructor. Create an `InitializeAsync()` method and call it from the Navigation logic.

## 2. Thread Safety (Critical)
- **Background Services:** If a class is a Singleton (like `SyncWorker`), NEVER inject a Scoped `DbContext`.
  - *Fix:* Inject `IServiceScopeFactory` and create a `using` scope inside the method.
- **UI Updates:** If a background service needs to update the UI, marshal the call to the UI Thread (`Application.Current.Dispatcher`).

## 3. Hardware I/O
- **Isolation:** Any call to WMI (`System.Management`) or Printers (`TcpClient`) must be wrapped in `Task.Run()`.
- **Timeouts:** Use `CancellationTokenSource(2000)` (2 seconds) for all hardware connections to prevent freezing.
