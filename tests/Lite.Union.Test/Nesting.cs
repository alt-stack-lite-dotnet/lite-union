using Lite.Union;

namespace Lite.Union.Test.Nesting;

public partial class ClassHost
{
    public partial struct StructHost
    {
        [Union<int, string>] public partial struct InStruct;
    }
}

public partial record RecordHost
{
    [Union<int, string>] public partial struct InRecord;
}

public partial record struct RecordStructHost
{
    [Union<int, string>] public partial struct InRecordStruct;
}
