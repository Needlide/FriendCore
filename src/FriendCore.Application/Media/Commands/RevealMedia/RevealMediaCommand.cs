using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Media.Commands.RevealMedia;

/// <summary>
/// Issues the variant URLs for a flagged media item, after checking the caller may see it.
///
/// This is the consent step for §7.4: flagged media carries no URLs in any list response
/// (see <see cref="MediaResponse.Create" />), and this is the only way to obtain them.
/// </summary>
public sealed record RevealMediaCommand(Ulid MediaId) : IRequest<ErrorOr<MediaVariantsResponse>>;
