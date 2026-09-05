using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Drafts;

public interface IDraftFlowService
{
    Task<DraftCreationResult> CreateAsync(CreateDraftRequest request, CancellationToken cancellationToken);
    Task<DraftSelectionResult> SelectTypeAsync(Guid draftId, long telegramUserId, Intent intent,
        DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task CancelAsync(Guid draftId, long telegramUserId, CancellationToken cancellationToken);
}
