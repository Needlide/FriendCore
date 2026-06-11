using FluentValidation;
using FriendCore.Domain.Media;

namespace FriendCore.Application.Media.Commands.UploadMedia;

public sealed class UploadMediaCommandValidator : AbstractValidator<UploadMediaCommand>
{
    public UploadMediaCommandValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0)
            .WithErrorCode("Media.FileRequired")
            .WithMessage("An image file is required.")
            .LessThanOrEqualTo(MediaErrors.MaxFileSizeBytes)
            .WithErrorCode("Media.FileTooLarge")
            .WithMessage("Image must not exceed 20MB.");

        RuleFor(x => x.ContentType)
            .Must(ImageContentTypes.IsAllowed)
            .WithErrorCode("Media.UnsupportedType")
            .WithMessage("Only JPEG, PNG, WebP, and GIF images are supported.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithErrorCode("Media.FileNameRequired")
            .WithMessage("File name is required.")
            .MaximumLength(255)
            .WithErrorCode("Media.FileNameTooLong")
            .WithMessage("File name must not exceed 255 characters.");
    }
}
