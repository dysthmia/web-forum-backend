using System.ComponentModel.DataAnnotations;

namespace Forum.Api.Features.Reactions;

public class SetReactionRequest
{
    [Range(-1, 1)]
    public int Type { get; init; }
}

public record ReactionResponse(long PostId, int Type);

public record ReactionCountsResponse(long PostId, int LikeCount, int DislikeCount, int? CurrentUserReaction);
