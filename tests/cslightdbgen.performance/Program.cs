using BenchmarkDotNet.Running;
using cslightdbgen.performance;

BenchmarkSwitcher.FromTypes(
[
    typeof(SingleInsertBenchmarks),
    typeof(BulkInsertBenchmarks),
    typeof(SingleRecordSelectBenchmarks),
    typeof(MultiRecordFilteredSelectBenchmarks),
    typeof(MultiRecordUnfilteredSelectBenchmarks)
]).Run(args);
