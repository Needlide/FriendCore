using FluentValidation;
using FriendCore.Application.Common.Behaviors;
using FriendCore.Application.Notifications.Services;
using FriendCore.Application.Posts.Common;
using FriendCore.Application.Tags.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = AssemblyReference.Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddSingleton<ITagNormalizer, TagNormalizationService>();
        services.AddScoped<PostMediaResponseFactory>();
        services.AddScoped<PostResponseAssembler>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}