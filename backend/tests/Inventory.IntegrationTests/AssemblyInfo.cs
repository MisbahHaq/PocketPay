using Xunit;

// Tests share one migrated database and reset it between tests, so parallel execution
// would let one test truncate another's rows mid-run. Correctness over wall-clock time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]