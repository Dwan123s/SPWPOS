using SPWPOS.Application.Workspace;
using SPWPOS.Domain.Entities;

namespace SPWPOS.Infrastructure.Workspace;

public sealed partial class WorkspaceService
{
    private static void ValidateEisenhowerPriority(string? importance)
    {
        Require(importance is null or "Important" or "Lower" or "Unknown",
            "INVALID_EISENHOWER_PRIORITY", "Chọn mức quan trọng hợp lệ.");
    }

    private void SetEisenhowerPriority(ITaskImportance task, string? importance, string source = "User")
    {
        ValidateEisenhowerPriority(importance);
        if (importance is not null)
            WriteImportance(task, new(importance, source, "Mức quan trọng được chọn khi tạo hoặc sửa công việc.", null, true), Now, source);
    }
}
