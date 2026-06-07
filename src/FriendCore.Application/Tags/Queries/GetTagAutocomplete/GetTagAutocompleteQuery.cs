using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Tags.Queries.GetTagAutocomplete;

public sealed record GetTagAutocompleteQuery(string Query) : IRequest<ErrorOr<TagAutocompleteResponse>>;