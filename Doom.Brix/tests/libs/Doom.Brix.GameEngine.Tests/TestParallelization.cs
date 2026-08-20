using Xunit.Sdk;
using Xunit.v3;

// The vendored engine's DummyData caches (reached through GameContent.CreateDummy)
// are process-static and not thread-safe, so test classes that build dummy
// content must not run concurrently. Serialize the whole assembly — it runs in
// about a second — matching the CodeBrix family's no-parallel test convention.
[assembly: Parallelization(Mode = ParallelMode.None)]
//was previously: [assembly: CollectionBehavior(DisableTestParallelization = true)]
//  (removed in xunit.v3 4.0)
