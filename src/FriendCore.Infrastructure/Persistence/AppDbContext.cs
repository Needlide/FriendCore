using System.Reflection;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteQueryFilters(modelBuilder);
    }

    /// <summary>
    ///     Applies <c>DeletedAt == null</c> filters to all entities implementing <see cref="ISoftDeletable" />.
    /// </summary>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(AppDbContext)
                .GetMethod(nameof(CreateSoftDeleteFilter),
                    BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(null, [modelBuilder]);
        }
    }

    private static void CreateSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => entity.DeletedAt == null);
    }
}