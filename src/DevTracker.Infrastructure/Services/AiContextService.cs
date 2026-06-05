using System.Text;
using DevTracker.Application.Abstractions.Services;
using DevTracker.Application.Common;

namespace DevTracker.Infrastructure.Services;

public sealed class AiContextService(IWorkItemService workItemService) : IAiContextService
{
    public async Task<Result<string>> PackTaskContextAsync(Guid workItemId, CancellationToken ct = default)
    {
        var result = await workItemService.GetByIdAsync(workItemId, ct);
        if (!result.Succeeded)
            return Result<string>.Fail(result.Error!);

        var wi = result.Value!;
        var sb = new StringBuilder();

        sb.AppendLine("# SOFTWARE DEVELOPMENT CONTEXT");
        sb.AppendLine();
        sb.AppendLine("You are acting as a senior AI assistant. The user needs help solving the following local task:");
        sb.AppendLine();
        sb.AppendLine("## Task Details");
        sb.AppendLine($"- **Number:** #{wi.Number}");
        sb.AppendLine($"- **Title:** {wi.Title}");
        sb.AppendLine($"- **Type:** {wi.Type}");
        sb.AppendLine($"- **Priority:** {wi.Priority}");
        sb.AppendLine();
        sb.AppendLine("## Description / Requirements");
        sb.AppendLine(string.IsNullOrWhiteSpace(wi.Description) ? "*(No description)*" : wi.Description);
        sb.AppendLine();
        sb.AppendLine("Instructions: Analyze the context above. When the user sends code or asks a question, focus strictly on solving these requirements in a clean and modular way.");

        return Result<string>.Success(sb.ToString());
    }
}
