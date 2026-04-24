using Disposal;

await using var foo = new Foo();
var bar = foo.Bar();
await Task.Delay(500);
await bar;
Console.WriteLine("Hello, World!");

class Foo : IAsyncDisposable
{
	private readonly DisposalTracker tracker;
	public Foo() => tracker = new(this);

	[Owned] private readonly Disposable disposable = new();

	[field: Owned]
	private Disposable DisposableProp { get; set; } = new();

	public async ValueTask DisposeAsync() => await tracker.DisposeAsync();

	public async Task Bar() => await tracker.Guard(async () =>
	{
		await Task.Delay(1000);
		Console.WriteLine("guard done");
	});
}

class Disposable : IDisposable
{
	public Disposable() => Console.WriteLine($"{nameof(Disposable)} created");
	public void Dispose() => Console.WriteLine($"{nameof(Disposable)} disposed");
}