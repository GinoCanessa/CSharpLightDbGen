using BenchmarkDotNet.Running;
using cslightdbgen.performance;

BenchmarkSwitcher.FromTypes(
[
    typeof(SingleInsertBenchmarks),
    typeof(BulkInsertThroughputBenchmarks),
    typeof(BulkInsertHydratingBenchmarks),
    typeof(SingleRecordSelectBenchmarks),
    typeof(MultiRecordFilteredSelectBenchmarks),
    typeof(MultiRecordUnfilteredSelectBenchmarks)
]).Run(args);
