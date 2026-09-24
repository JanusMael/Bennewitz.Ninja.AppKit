// Tests in this assembly run one at a time.
//
// ⛔ Ported from the original suite's [assembly: DoNotParallelize], and required for the same
// reason: there is ONE headless Avalonia dispatcher per assembly, and it must not be driven
// concurrently. MSTest ran that suite serially; xunit v3 parallelises across classes by default,
// so without this the port would introduce races the originals never had.
// Headless/HeadlessSessionTests.cs guards the premise: one session, so one dispatcher, per assembly.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
