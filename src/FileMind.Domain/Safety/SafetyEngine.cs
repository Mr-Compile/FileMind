using FileMind.Domain.Contracts;
using FileMind.Domain.Ports;

namespace FileMind.Domain.Safety;

public sealed record SafetyContext(
    IProtectedPathProvider ProtectedPaths,
    IReadOnlyDictionary<string, PermissionCapabilities>? CapabilityOverrides = null,
    IReadOnlyDictionary<string, long>? FreeSpaceOverrides = null);

public sealed class SafetyEngine(IFileSystemAdapter fs, IPathNormalizer paths)
{
    public async Task<SafetyResult> ValidateAsync(ActionPlan plan, SafetyContext context, CancellationToken ct = default)
    {
        var blockers = new List<string>();
        var warnings = new List<string>();
        string? confirmation = null;

        if (plan.Items.Count == 0)
            blockers.Add("Plan has no items.");

        var itemsNeedingConfirmation = new List<string>();

        foreach (var item in plan.Items)
        {
            if (!item.IsValid)
            {
                blockers.Add($"Item '{item.Source}': {item.Operation} requires a destination.");
                continue;
            }

            var caps = context.CapabilityOverrides?.GetValueOrDefault(item.Source);

            if (context.ProtectedPaths.IsProtected(paths.Normalize(item.Source)))
                blockers.Add($"'{item.Source}': source is a protected path.");
            if (item.Destination is not null && context.ProtectedPaths.IsProtected(paths.Normalize(item.Destination)))
                blockers.Add($"'{item.Destination}': destination is a protected path.");

            if (item.Operation is OperationType.Move or OperationType.Copy or OperationType.Rename or OperationType.CreateFolder)
            {
                var stat = await fs.StatAsync(item.Source, ct);
                if (!stat.IsSuccess && item.Operation != OperationType.CreateFolder)
                    blockers.Add($"'{item.Source}': {stat.Error!.Message}");
            }

            if (item.Destination is not null && item.Operation != OperationType.CreateFolder)
            {
                var destExists = await fs.ExistsAsync(item.Destination, ct);
                if (destExists is { IsSuccess: true, Value: true })
                {
                    switch (item.ConflictPolicy)
                    {
                        case ConflictPolicy.KeepBoth:
                            warnings.Add($"'{item.Destination}' exists; will keep both with deterministic suffix.");
                            break;
                        case ConflictPolicy.Skip:
                            warnings.Add($"'{item.Destination}' exists; item will be skipped.");
                            break;
                        case ConflictPolicy.Ask:
                            itemsNeedingConfirmation.Add($"'{item.Destination}' exists; overwrite decision required.");
                            break;
                        case ConflictPolicy.ReplaceWithConfirmation:
                            itemsNeedingConfirmation.Add($"'{item.Destination}' will be replaced; confirmation required.");
                            break;
                    }
                }

                var free = context.FreeSpaceOverrides?.GetValueOrDefault(item.Destination);
                var stat2 = await fs.StatAsync(item.Source, ct);
                if (free is not null && stat2.IsSuccess && free.Value < stat2.Value.SizeBytes)
                    blockers.Add($"'{item.Destination}': insufficient free space ({free.Value} < {stat2.Value.SizeBytes}).");
            }

            if (caps is not null)
            {
                if (item.Operation is OperationType.Move or OperationType.Rename && !caps.CanMove)
                    blockers.Add($"'{item.Source}': move not permitted by capabilities.");
                if (item.Operation == OperationType.Copy && !caps.CanCopy)
                    blockers.Add($"'{item.Source}': copy not permitted by capabilities.");
            }
        }

        if (itemsNeedingConfirmation.Count > 0)
            confirmation = string.Join(' ', itemsNeedingConfirmation);

        return new SafetyResult
        {
            Allowed = blockers.Count == 0,
            Blockers = blockers,
            Warnings = warnings,
            RequiredConfirmation = confirmation
        };
    }
}
