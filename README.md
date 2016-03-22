# Disposal

A tool I created to reduce the amount of code needed to implement `IDisposable` correctly and with thread-safety.

Correctly disposing managed and unmanaged resources requires careful consideration of several factors including:

- freeing unmanaged handles
- calling `Dispose` on all `IDisposable` members
- ensuring handles and members are not disposed more than once
- suppressing the GC attempt to finalize the object after it's been explicitly disposed
- protecting against race-conditions where the object could be disposing while another thread is calling a method on the object, and vice versa
- ensuring different threads always see the latest state of the disposable object and its members

Disposal takes care of all of these considerations with the following support:

- Automatically call `Dispose()` on all `IDisposable` members (this is done by emitting and caching IL; it is as fast as hand-written code)
- Set disposed members to null in a thread-safe manner
- Provide a simple wrapper for your methods to prevent disposing while in use, and to prevent method use while disposing/disposed
- Even if you don't guard all of your methods, your `IDisposable` members will be set to null so calling code can't do anything terrible

## Installation

Install the NuGet package which targets both .NET 4.5.1 and .NET Core or, of course, clone/fork this repository and build the assembly yourself!

## Usage

`DisposableTracker<T>` is the core class you'll be using. It keeps track of the disposal state and provides a few methods which provide safe and correct behaviour.

**Basic**

```csharp
class Foo : IDisposable {
	// IDisposable members here...

	private DisposableTracker<Foo> disposableTracker;
	public void Dispose() => disposableTracker.Dispose(this);

	public void Bar() => disposableTracker.Guard(() => {
		// Your normal Bar() implementation here...
	});
}
```

**Full**

```csharp
class Foo : IDisposable {
	// These each implement `IDisposable`
	private SomeClass sc = new SomeClass();
	private SomeStruct ss = new SomeStruct();
	private SomeClassEx scx = new SomeClassEx();
	private SomeStructEx ssx = new SomeStructEx();
	private SafeHandle someUnmanagedResource; // assign some derived class instance which wraps your unmanaged resource

	private DisposableTracker<Foo> disposableTracker;
	public void Dispose() => disposableTracker.Dispose(this); // `Dispose` and assigns null to each member, then call `GC.SuppressFinailize()`
	~Foo() { disposableTracker.Dispose(this); }

	public void DoSomething() => disposableTracker.DisposalGuard(() => {
		// Foos won't be disposed while we're inside a guard, and will throw ObjectDisposedException if the object is disposed while trying to enter a guard
		bar.DoAThing(baz);
	});

	public void UseARefOrOutParam(ref Int32 number) {
		// We need to explicitly enter and exit a guard here since you can't close over ref/out inside lambdas
		try {
			disposableTracker.EnterGuard();

			number += 42;
		}
		finally {
			disposableTracker.ExitGuard();
		}
	}
}

class SomeClass : IDisposable {
	public void Dispose() => Console.WriteLine("Dispose this Foo!");
}
struct SomeStruct : IDisposable {
	public void Dispose() => Console.WriteLine("Dispose this Bar!");
}
// Explicit implementation of `Dispose`
class SomeClassEx : IDisposable {
	void IDisposable.Dispose() => Console.WriteLine("Dispose this FooEx through IDisposable!");
}
struct SomeStructEx : IDisposable {
	void IDisposable.Dispose() => Console.WriteLine("Dispose this BarEx through IDisposable!");
}
```

### Remarks

- Classes are handled as if calling `Interlocked.Exchange(ref @this.someClass, null)?.Dispose();`
- Unmanaged handles are not freed; this is basically impossible to do automatically. It is strongly suggested that you always wrap your unmanaged handles in a class derived from [SafeHandle](https://msdn.microsoft.com/en-us/library/system.runtime.interopservices.safehandle(v=vs.110).aspx).
- Structs which implement `IDisposable` need to use `DisposableStructTracker<T>`
- Structs can implement `IDisposable.Dispose()` explicitly (through interface only) and Disposal will call it without boxing (this is good)

## TODO

- Handle calling a base class `Dispose` method in a way that makes sense

## Contributing

1. [Create an issue](https://github.com/NickStrupat/Disposal/issues/new)
2. Let's find some point of agreement on your suggestion.
3. Fork it!
4. Create your feature branch: `git checkout -b my-new-feature`
5. Commit your changes: `git commit -am 'Add some feature'`
6. Push to the branch: `git push origin my-new-feature`
7. Submit a pull request :D

## History

[Commit history](https://github.com/NickStrupat/Disposal/commits/master)

## License

MIT License