; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
LUNION001 | Lite.Union | Error | Union type must be partial
LUNION002 | Lite.Union | Error | Duplicate Union case name
LUNION003 | Lite.Union | Error | Union type must not be generic
LUNION004 | Lite.Union | Error | Union type must not be a record struct
LUNION005 | Lite.Union | Error | Unsafe code is required
LUNION006 | Lite.Union | Error | Cases cannot be told apart
LUNION007 | Lite.Union | Warning | Unknown project setting
LUNION100 | Lite.Union | Info | Case is not overlapped
