using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Queries.GetMyProfileTags;

public sealed record GetMyProfileTagsQuery : IRequest<ErrorOr<ProfileTagsResponse>>;
