using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Queries.GetTagDetails;

public sealed record GetTagDetailsQuery(string Slug) : IRequest<ErrorOr<TagResponse>>;
