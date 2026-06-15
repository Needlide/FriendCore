using System.Reflection;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Notifications;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService? currentUserService = null) : DbContext(options)
{
    /// <summary>
    ///     Named query filters (EF 10). Both apply to every query by default; a specific one can
    ///     be skipped with <c>IgnoreQueryFilters([AppDbContext.BlocksFilter])</c> while the other
    ///     keeps protecting the query.
    /// </summary>
    public const string SoftDeleteFilter = "SoftDelete";

    public const string BlocksFilter = "Blocks";

    public DbSet<User> Users => Set<User>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Mute> Mutes => Set<Mute>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AdminAuditLog> AdminAuditLog => Set<AdminAuditLog>();
    public DbSet<Appeal> Appeals => Set<Appeal>();

    // Referenced inside the block filters. The model (and the filter expressions in it) is built
    // ONCE and cached for the lifetime of the app; EF rewrites accesses to this property so each
    // query reads it from the context instance that is executing, as a query parameter. That is
    // what turns one static filter definition into a per-request predicate. Null outside an HTTP
    // request (background services, design time) — the filters then pass everything through.
    private Ulid? CurrentUserId => currentUserService?.UserId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteQueryFilters(modelBuilder);
        ApplyBlockQueryFilters(modelBuilder);
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
        modelBuilder.Entity<TEntity>().HasQueryFilter(SoftDeleteFilter, entity => entity.DeletedAt == null);
    }

    /// <summary>
    ///     §8.2: content from users with a block relationship (either direction) with the current
    ///     user is excluded from every query automatically — no handler has to remember blocks.
    ///     Per spec the filter covers User, Post, and Comment; Likes are deliberately not
    ///     filtered (counts may include blocked users — accepted MVP trade-off).
    /// </summary>
    private void ApplyBlockQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(BlocksFilter, user =>
            CurrentUserId == null
            || !Blocks.Any(b =>
                (b.BlockerId == CurrentUserId && b.BlockedId == user.Id)
                || (b.BlockerId == user.Id && b.BlockedId == CurrentUserId)));

        modelBuilder.Entity<Post>().HasQueryFilter(BlocksFilter, post =>
            CurrentUserId == null
            || !Blocks.Any(b =>
                (b.BlockerId == CurrentUserId && b.BlockedId == post.AuthorId)
                || (b.BlockerId == post.AuthorId && b.BlockedId == CurrentUserId)));

        modelBuilder.Entity<Comment>().HasQueryFilter(BlocksFilter, comment =>
            CurrentUserId == null
            || !Blocks.Any(b =>
                (b.BlockerId == CurrentUserId && b.BlockedId == comment.AuthorId)
                || (b.BlockerId == comment.AuthorId && b.BlockedId == CurrentUserId)));
    }
}
