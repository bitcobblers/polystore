using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

// Minimal config: write BDN's artifacts (results + logs) under the already-gitignored
// artifacts/ directory. BDN's own default is BenchmarkDotNet.Artifacts/, which the repo's
// .gitignore does NOT cover — so without this, results would be committed (D7/A4).
// A single ArtifactsPath setting is a justified, minimal config, not a configuration layer (C1).
var config = DefaultConfig.Instance.WithArtifactsPath("artifacts");

// Discover and run every [Benchmark] class in this assembly.
// Command-line arguments (--filter, --list, --exporters, …) are passed through from
// `dotnet run -- …`.
// Overload: BenchmarkRunner.Run(Assembly, IConfig?, string[]?) — note the args are the
// LAST parameter (the invalid form is Run(args, config); there is no such overload).
BenchmarkRunner.Run(typeof(Program).Assembly, config, args);
