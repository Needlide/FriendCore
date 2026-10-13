using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using FriendCore.Application.Common.Interfaces;

namespace FriendCore.Application.Moderation.Services;

/// <summary>
///     Builds the subject snapshot each report carries, per target type.
/// </summary>
/// <remarks>
/// <para>
///     Every lookup here deliberately bypasses the ordinary visibility filters — a block filter or a
///     soft-delete filter would mean the snapshot comes back empty for exactly the cases a
///     moderator most needs it: a report filed about content that has since been deleted.
/// </para>
/// <para>
///     Returns null rather than throwing when the subject is gone. The queue renders that as
///     "content unavailable", which is honest, instead of a blank row that reads like a capture
///     bug.
/// </para>
/// </remarks>
internal sealed class SubjectSnapshotFactory(
    IPostRepository postRepository,
    IMessagingRepository messagingRepository,
    IMediaRepository mediaRepository,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider)
    : ISubjectSnapshotFactory
{
    /// <summary>
    ///     Upper bound on a captured body. A moderation queue needs enough to judge a report, not a
    ///     complete archive of someone's writing — and an unbounded copy of reported content is
    ///     its own retention problem.
    /// </summary>
    private const int MaxCapturedLength = 2000;

    public async Task<ReportSubjectSnapshot?> CaptureAsync(
        ReportTargetType targetType,
        Ulid targetId,
        CancellationToken cancellationToken = default)
    {
        var capturedAt = dateTimeProvider.UtcNow;

        return targetType switch
        {
            ReportTargetType.Post => await CapturePostAsync(targetId, capturedAt, cancellationToken),
            ReportTargetType.Message => await CaptureMessageAsync(targetId, capturedAt, cancellationToken),
            ReportTargetType.Media => await CaptureMediaAsync(targetId, capturedAt, cancellationToken),
            ReportTargetType.User => await CaptureUserAsync(targetId, capturedAt, cancellationToken),
            _ => null
        };
    }

    private async Task<ReportSubjectSnapshot?> CapturePostAsync(
        Ulid postId,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        // GetByIdForModerationAsync ignores the block and soft-delete filters on purpose.
        var post = await postRepository.GetByIdForModerationAsync(postId, cancellationToken);
        if (post is null)
            return null;

        var authorName = await UsernameOfAsync(post.AuthorId, cancellationToken);

        // A media-only post has no text. The snapshot is still worth taking — the caption is empty,
        // but the AUTHOR and the FACT that it existed are what a queue row needs, and dropping the
        // snapshot entirely would show "content unavailable" for something perfectly available.
        return new ReportSubjectSnapshot(
            ReportSubjectKind.Text,
            Truncate(post.Content ?? string.Empty),
            post.AuthorId,
            authorName,
            capturedAt);
    }

    private async Task<ReportSubjectSnapshot?> CaptureMessageAsync(
        Ulid messageId,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        // GetMessageForModerationAsync ignores the soft-delete filter, which is the point: a report
        // about a message either party has since deleted still has to be reviewable.
        var message = await messagingRepository.GetMessageForModerationAsync(messageId, cancellationToken);
        if (message is null)
            return null;

        var authorName = await UsernameOfAsync(message.SenderId, cancellationToken);

        return new ReportSubjectSnapshot(
            ReportSubjectKind.Text,
            Truncate(message.Content ?? string.Empty),
            message.SenderId,
            authorName,
            capturedAt);
    }

    private async Task<ReportSubjectSnapshot?> CaptureMediaAsync(
        Ulid mediaId,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        var media = await mediaRepository.GetByIdAsync(mediaId, cancellationToken);
        if (media is null)
            return null;

        // Deliberately NOT the image bytes. Media is soft-deleted and its object stays retrievable,
        // so a moderator can still open the actual file; copying it here would be a second copy of
        // the very content the report is asking to be taken down.
        var description = $"{media.OriginalFileName} ({media.ContentType})";

        return new ReportSubjectSnapshot(
            ReportSubjectKind.Image,
            description,
            media.UploaderId,
            await UsernameOfAsync(media.UploaderId, cancellationToken),
            capturedAt);
    }

    private async Task<ReportSubjectSnapshot?> CaptureUserAsync(
        Ulid userId,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return null;

        // The user IS the subject, so the author is them.
        return new ReportSubjectSnapshot(
            ReportSubjectKind.Username,
            user.Username,
            user.Id,
            user.Username,
            capturedAt);
    }

    private async Task<string?> UsernameOfAsync(Ulid userId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user?.Username;
    }

    /// <summary>
    ///     Trims with an ellipsis rather than cutting mid-word, so a reviewer can see that there is
    ///     more than they are seeing.
    /// </summary>
    private static string Truncate(string content) =>
        content.Length <= MaxCapturedLength ? content : content[..MaxCapturedLength] + "…";
}