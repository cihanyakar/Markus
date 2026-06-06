using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// Run every benchmark class with no arguments, or pass BenchmarkDotNet filters
// such as `--filter *WordCount*` to narrow the run:
//   PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" \
//     ~/.dotnet/dotnet run -c Release --project benchmarks/Markus.Benchmarks
//
// The in-process toolchain runs benchmarks inside this already-optimized
// process. It is required here because the .NET 11 preview runtime moniker is
// not recognized by this BenchmarkDotNet version's CsProj SDK validator, which
// would otherwise abort the run before any measurement.
var config = DefaultConfig.Instance.AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));

var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
if (args.Length == 0)
{
    switcher.RunAllJoined(config);
}
else
{
    switcher.Run(args, config);
}
