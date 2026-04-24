# Disposal

A utility library that reduces the boilerplate needed to implement `IAsyncDisposable` correctly and with thread-safety.

## Why?

Correctly disposing resources requires careful consideration of several factors including:

- Calling `Dispose()` or `DisposeAsync()` on all disposable members
- Ensuring members are not disposed more than once
- Protecting against race-conditions where the object could be disposing while another thread is calling a method on the object, and vice versa

Disposal takes care of these considerations for you:

- Automatically calls `DisposeAsync()` on `IAsyncDisposable` fields and `Dispose()` on `IDisposable` fields marked with `[Owned]`
- Provides a guard mechanism that prevents method calls while disposing/disposed, and prevents disposal while guarded methods are in-flight
- `DisposeAsync()` is idempotent — calling it multiple times is safe
- Uses lock-free atomics for the guard counter, so there's no contention in the common case

### Thread-safety guarantees

The guard and disposal operations are fully atomic with respect to each other:

- **Guard entry is atomic** — the use-count is incremented before the status check, so `DisposeAsync()` always sees the true count of active guards. There is no window where a guard could be running while fields are being disposed.
- **Disposal waits** — `DisposeAsync()` will not begin disposing fields until all active guards have exited.
- **Late guard attempts fail** — if `DisposeAsync()` has been called, new guard attempts throw `ObjectDisposedException`.

## Installation

Install the [NuGet package](https://www.nuget.org/packages/Disposal/) or clone/fork this repository and build it yourself.

## Usage

`DisposalTracker` is the core class. Add it as a field, delegate `DisposeAsync()` to it, and wrap your methods in `Guard` calls.

### Basic

Mark fields you own with `[Owned]` — they'll be disposed automatically. Unmarked fields (e.g. injected dependencies) are left alone.

```csharp
class Foo : IAsyncDisposable
{
    [Owned] private readonly SomeResource resource = new();
    private readonly DisposalTracker tracker;

    public Foo() => tracker = new(this);

    public ValueTask DisposeAsync() => tracker.DisposeAsync();

    public void Bar() => tracker.Guard(() =>
    {
        // Your normal Bar() implementation here.
        // Throws ObjectDisposedException if Foo is disposing/disposed.
        // Disposal will wait for this to complete before proceeding.
    });
}
```

### Async guards

```csharp
class Foo : IAsyncDisposable
{
    [Owned] private readonly HttpClient client = new();
    private readonly DisposalTracker tracker;

    public Foo() => tracker = new(this);

    public ValueTask DisposeAsync() => tracker.DisposeAsync();

    public async Task<string> FetchAsync(string url) => await tracker.GuardAsync(async () =>
    {
        return await client.GetStringAsync(url);
    });
}
```

### Owned vs. injected fields

Only fields marked `[Owned]` are disposed. Injected dependencies require no annotation — they're ignored by default.

```csharp
class Foo : IAsyncDisposable
{
    private readonly ILogger logger; // injected, not disposed

    [Owned] private readonly Stream ownedStream = new MemoryStream();
    private readonly DisposalTracker tracker;

    public Foo(ILogger logger)
    {
        this.logger = logger;
        tracker = new(this);
    }

    public ValueTask DisposeAsync() => tracker.DisposeAsync();
}
```

For auto-implemented properties, use `[field: Owned]` to target the backing field:

```csharp
[field: Owned] public Stream Data { get; set; } = new MemoryStream();
```

### Guard methods

| Method | Signature |
|---|---|
| `Guard` | `void Guard(Action body)` |
| `Guard<T>` | `T Guard<T>(Func<T> body)` |
| `GuardAsync` | `Task GuardAsync(Func<Task> body)` |
| `GuardAsync<T>` | `Task<T> GuardAsync<T>(Func<Task<T>> body)` |

All guard methods throw `ObjectDisposedException` if the object is disposing or disposed. `DisposeAsync()` will wait for all active guards to complete before disposing fields.

## Remarks

- Only fields marked with `[Owned]` are disposed. Unmarked disposable fields are left alone.
- For auto-implemented properties, use `[field: Owned]` to target the compiler-generated backing field.
- `DisposalTracker` fields are always skipped, even if marked `[Owned]`.
- Field getters are compiled via expression trees and cached per type, so reflection cost is paid only once.
- Unmanaged handles are not freed automatically. Wrap them in a class derived from [SafeHandle](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.safehandle).

## Contributing

1. [Create an issue](https://github.com/NickStrupat/Disposal/issues/new)
2. Let's find some point of agreement on your suggestion.
3. Fork it!
4. Create your feature branch: `git checkout -b my-new-feature`
5. Commit your changes: `git commit -am 'Add some feature'`
6. Push to the branch: `git push origin my-new-feature`
7. Submit a pull request

## License

[MIT License](LICENSE)