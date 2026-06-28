# Async in Unity — From a .NET Developer's Perspective

## The Short Version

Unity has its own `Task` equivalent called `Awaitable`. You write `async Awaitable` instead of `async Task`. Everything else — `await`, `CancellationToken`, fire-and-forget with `_ =` — works the same as your day job.

## The Three Eras

### 1. Unity Coroutines (2005–present, legacy)

```csharp
IEnumerator DoThing()
{
    Debug.Log("Start");
    yield return new WaitForSeconds(1f);
    Debug.Log("After 1 second");
    yield return StartCoroutine(OtherThing());
    Debug.Log("After other thing");
}

// Called with:
StartCoroutine(DoThing());
```

**Why you'll see this everywhere:**
- Every Unity tutorial, every Stack Overflow answer, every asset store plugin uses it
- The entire Unity ecosystem built on it for 20 years
- Still works fine, not deprecated

**Why it's ugly:**
- Abuses C#'s `IEnumerator`/`yield return` iterator pattern for something it wasn't designed for
- No return values, no exception handling, no cancellation (without hacks)
- Can't use `try/catch` across yield points
- `StartCoroutine()` only works on MonoBehaviours

### 2. async Task (avoid in Unity)

```csharp
async Task DoThing()
{
    Debug.Log("Start");
    await Task.Delay(1000);
    Debug.Log("After 1 second — BUT MAYBE ON WRONG THREAD!");
}
```

**Why this is dangerous in Unity:**
- `Task.Delay` may resume on a thread pool thread
- Unity APIs (Transform, GameObject, etc.) are main-thread only
- Accessing Unity objects from a background thread = crash
- You *can* make it work with `SynchronizationContext` but it's fragile

### 3. async Awaitable (Unity 6+, use this)

```csharp
async Awaitable DoThing()
{
    Debug.Log("Start");
    await Awaitable.WaitForSecondsAsync(1f);
    Debug.Log("After 1 second — guaranteed main thread");
}

// Called with:
_ = DoThing();          // fire and forget
await DoThing();        // from another async Awaitable
```

**Why this is right:**
- Guaranteed main thread (like coroutines)
- Proper `async/await` syntax (like your day job)
- Supports `CancellationToken`
- Supports `try/catch`
- Can return values (`async Awaitable<int>`)
- Lightweight (pooled, less allocation than Task)

## Mapping to Your Day Job

| .NET 8 (ASP.NET Core) | Unity 6 | Purpose |
|---|---|---|
| `async Task` | `async Awaitable` | Async method |
| `async Task<T>` | `async Awaitable<T>` | Async method with return value |
| `await Task.Delay(ms)` | `await Awaitable.WaitForSecondsAsync(s)` | Pause execution |
| `await task` | `await awaitable` | Wait for completion |
| `_ = DoAsync()` | `_ = DoAsync()` | Fire and forget |
| `CancellationToken` | `CancellationToken` | Cooperative cancellation |
| `cts.Cancel()` | `cts.Cancel()` | Signal cancellation |
| N/A | `await Awaitable.NextFrameAsync()` | Wait one render frame |
| N/A | `await Awaitable.EndOfFrameAsync()` | Wait until end of frame |
| N/A | `await Awaitable.FixedUpdateAsync()` | Wait for next physics step |

## The yield return / IEnumerator Connection

You see `yield return` in two completely different contexts:

### Context A — Data iteration (your day job, still valid everywhere)

```csharp
IEnumerable<int> GetNumbers()
{
    yield return 1;
    yield return 2;
    yield return 3;
}

// Lazily produces values one at a time
foreach (var n in GetNumbers()) { ... }
```

This is **not async**. It's lazy evaluation — the method pauses at each `yield return` and resumes when the caller asks for the next item. Used for LINQ, streaming data, custom iterators. Still 100% relevant in .NET 8.

### Context B — Unity coroutines (legacy async hack)

```csharp
IEnumerator WaitAndLog()
{
    yield return new WaitForSeconds(2f);
    Debug.Log("Done");
}
```

Unity's coroutine scheduler calls `MoveNext()` on the iterator each frame, inspects the yielded value to decide how long to wait, then calls `MoveNext()` again. It's a creative abuse of the iterator pattern to simulate async before `async/await` existed.

**Same C# feature, completely different purpose.** When you see `IEnumerator` + `yield return` in Unity code, it's async control flow, not data iteration.

## When You See Legacy Coroutines on Stack Overflow

Translate mentally:

| Coroutine | Modern equivalent |
|---|---|
| `IEnumerator Method()` | `async Awaitable Method()` |
| `yield return new WaitForSeconds(x)` | `await Awaitable.WaitForSecondsAsync(x)` |
| `yield return null` | `await Awaitable.NextFrameAsync()` |
| `yield return StartCoroutine(X())` | `await X()` |
| `StartCoroutine(Method())` | `_ = Method()` |
| `StopCoroutine(ref)` | `cts.Cancel()` |
| `yield break` | `return` |

## Our Project's Pattern

```csharp
// Fire-and-forget from a synchronous callback (e.g. SignalR event)
void OnSomeEvent(string data)
{
    _ = HandleEvent(data);
}

// Async method with sequenced steps
async Awaitable HandleEvent(string data)
{
    ShowUI();
    await DoAnimation();
    await Awaitable.WaitForSecondsAsync(1f);
    HideUI();
}

// Cancellable loop (e.g. pulse animation)
async Awaitable PulseLoop(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        // animate...
        await Awaitable.NextFrameAsync();
    }
}
```

## Summary

- Use `async Awaitable` for all new Unity async code
- Ignore coroutines in tutorials — mentally translate to async/await
- `yield return` in .NET data code (IEnumerable) is still perfectly valid and unrelated
- Unity's `Awaitable` = .NET's `Task` but main-thread-safe and frame-aware
