using ErrorOr;
using Microsoft.AspNetCore.Mvc;

namespace FriendCore.API.Extensions;

public static class ErrorOrExtensions
{
    public static IActionResult FromErrors(List<Error> errors)
    {
        return ToErrorActionResult(errors);
    }

    private static ActionResult ToErrorActionResult(List<Error> errors)
    {
        return errors.First().Type switch
        {
            ErrorType.NotFound => new NotFoundObjectResult(errors),
            ErrorType.Conflict => new ConflictObjectResult(errors),
            ErrorType.Validation => new BadRequestObjectResult(errors),
            ErrorType.Forbidden => new ForbidResult(),
            ErrorType.Unauthorized => new UnauthorizedResult(),
            _ => new ObjectResult(errors) { StatusCode = StatusCodes.Status500InternalServerError }
        };
    }

    extension<T>(ErrorOr<T> result)
    {
        // Ok result, 200 response code
        public IActionResult ToActionResult()
        {
            return result.Match<ActionResult>(
                value => new OkObjectResult(value),
                ToErrorActionResult);
        }

        // Created result, 201 response code
        public IActionResult ToCreatedResult()
        {
            return result.Match<ActionResult>(
                value => new CreatedResult(string.Empty, value),
                ToErrorActionResult);
        }
    }
}