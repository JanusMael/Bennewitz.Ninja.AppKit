// Tests in this assembly run one at a time.
//
// ⛔ Ported from the original suite's [assembly: DoNotParallelize], and required for the same
// reason: there is ONE headless Avalonia dispatcher per assembly, and it must not be driven
// concurrently. MSTest ran that suite serially; xunit v3 parallelises across classes by default,
// so without this the port would introduce races the originals never had. HeadlessSessionBootstrap
// decides when the dispatcher comes up; this keeps it single-threaded once it has.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
