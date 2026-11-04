using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Tags;
using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FriendCore.Infrastructure.Persistence;

/// <summary>
///     Optional demo dataset for preview/staging deployments.
///     <para>
///         This is deliberately NOT sample data for production. It is opt-in twice: the host must
///         be started with <c>--seed-demo</c> (or <c>Seeding:DemoData=true</c>), and the seeder
///         refuses to run against a database that already has users. A preview environment can
///         therefore be re-seeded from an empty database without any risk of duplicating rows or
///         touching real accounts.
///     </para>
///     <para>
///         Everything goes through the domain factories, not raw SQL: password hashing, username
///         slugs, tag counters, like/comment counts and conversation keys all take the same code
///         paths as real traffic. Seeded accounts are marked verified so employees can sign in
///         without an email provider.
///     </para>
/// </summary>
public static class DemoDataSeeder
{
    private const string EmailDomain = "preview.friendcore.local";
    private const string DefaultPassword = "Preview123!";

    private sealed record SeedUser(
        string Username,
        string DisplayName,
        string Bio,
        string Location,
        UserRole Role = UserRole.User);

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        // Idempotence guard: a preview deployment may restart (or re-run the command) any number
        // of times. Never append a second copy of the dataset.
        if (await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Demo seed skipped — the database already contains users.");
            return;
        }

        var passwordHasher = provider.GetRequiredService<IPasswordHasher>();
        var emailHashService = provider.GetRequiredService<IEmailHashService>();
        var seeding = provider.GetRequiredService<IOptions<SeedingOptions>>().Value;
        var password = string.IsNullOrWhiteSpace(seeding.DemoPassword) ? DefaultPassword : seeding.DemoPassword;
        var now = DateTimeOffset.UtcNow;

        // ---------------------------------------------------------------- users
        SeedUser[] definitions =
        [
            new("maya", "Maya Chen", "Product designer. Photos of ordinary light.", "Lisbon"),
            new("theo", "Theo Alvarez", "Backend by day, film camera by night.", "Barcelona"),
            new("june", "June Park", "Books, long walks, and too many open tabs.", "Seoul"),
            new("kai", "Kai Nakamura", "Coffee and small adventures.", "Tokyo"),
            new("noor", "Noor Haddad", "Architecture, cities, and the people in them.", "Amman"),
            new("sam", "Sam Okafor", "Running slowly, eating well.", "Lagos"),
            new("lena", "Lena Fischer", "Illustrator. Draws chairs and cats.", "Berlin"),
            new("omar", "Omar Farouk", "Music producer. Always listening.", "Cairo"),
            new("admin", "FriendCore Preview", "Preview administrator account.", "The internet", UserRole.Admin)
        ];

        var users = new Dictionary<string, User>(StringComparer.Ordinal);
        var preferences = new List<UserPreferences>(definitions.Length);

        foreach (var definition in definitions)
        {
            var email = $"{definition.Username}@{EmailDomain}";
            var user = User.Register(
                definition.Username,
                definition.DisplayName,
                emailHashService.Hash(User.NormalizeEmail(email)),
                email,
                passwordHasher.Hash(password),
                now).Value;

            user.MarkEmailVerified(now);
            user.UpdateProfile(null, definition.Bio, null, definition.Location, now);
            user.AssignRole(definition.Role);

            users[definition.Username] = user;
            preferences.Add(UserPreferences.CreateDefault(user.Id, now));
        }

        db.Users.AddRange(users.Values);
        db.UserPreferences.AddRange(preferences);

        // ----------------------------------------------------------------- tags
        (string Name, string Slug)[] tagDefinitions =
        [
            ("Photography", "photography"),
            ("Travel", "travel"),
            ("Books", "books"),
            ("Music", "music"),
            ("Coffee", "coffee"),
            ("Hiking", "hiking"),
            ("Design", "design"),
            ("Food", "food"),
            ("Running", "running"),
            ("Film", "film"),
            ("Gaming", "gaming"),
            ("Art", "art")
        ];

        var tags = new Dictionary<string, Tag>(StringComparer.Ordinal);
        foreach (var (name, slug) in tagDefinitions)
        {
            var tag = Tag.Create(name, slug, users["admin"].Id, now);
            tag.Approve();
            tags[slug] = tag;
        }

        db.Tags.AddRange(tags.Values);

        // -------------------------------------------------------- profile tags
        Dictionary<string, string[]> profileTags = new(StringComparer.Ordinal)
        {
            ["maya"] = ["photography", "design"],
            ["theo"] = ["film", "music"],
            ["june"] = ["books", "coffee"],
            ["kai"] = ["coffee", "travel"],
            ["noor"] = ["design", "travel"],
            ["sam"] = ["running", "food"],
            ["lena"] = ["art", "design"],
            ["omar"] = ["music", "gaming"],
            ["admin"] = ["design"]
        };

        foreach (var (username, slugs) in profileTags)
        foreach (var (slug, index) in slugs.Select((slug, index) => (slug, index)))
            users[username].AddProfileTag(
                tags[slug].Id,
                index == 0 ? TagTier.Primary : TagTier.Secondary,
                isPublic: true,
                now);

        // ---------------------------------------------------------------- posts
        (string Author, string Content, string[] Tags, int MinutesAgo)[] postDefinitions =
        [
            ("maya", "Spent the morning photographing the same corner of the apartment at different hours. The 8am light is doing something the camera refuses to explain.", ["photography", "design"], 14),
            ("theo", "Rewrote a query three times today and the last version is just the first one with an index. Feeling personally attacked by my own past self.", ["design"], 38),
            ("june", "Finished a novel that made the last ten minutes of my commute feel like a different city. Recommendations welcome — especially anything with weather in it.", ["books"], 61),
            ("kai", "New coffee place opened downstairs. The espresso is good, the playlist is better, and I am now permanently five minutes late for everything.", ["coffee", "food"], 95),
            ("noor", "A building is never just a building. It is a hundred small decisions about how people will move through a day.", ["design", "travel"], 140),
            ("sam", "Ran 7k without stopping for the first time since spring. Slowly is still a speed.", ["running"], 178),
            ("lena", "Today's warm-up sketch: a chair that is definitely judging me.", ["art", "design"], 205),
            ("omar", "Found a bass line buried in an old project file. Half the song is there already; I just hadn't listened to it in the right mood.", ["music"], 260),
            ("maya", "The best part of a design review is the five minutes afterwards, when everyone stops performing and starts thinking out loud.", ["design"], 320),
            ("theo", "Hot take: most 'scalability problems' are observability problems wearing a trench coat.", ["design"], 410),
            ("june", "Bookshop rule: you may leave with one book you came for and one you did not know existed.", ["books", "coffee"], 500),
            ("kai", "Took the long way home through the park. Added twelve minutes and subtracted the entire week.", ["travel", "hiking"], 610),
            ("noor", "Sketching façades again. The interesting part is always the ground floor, where the building has to negotiate with the street.", ["design", "art"], 720),
            ("sam", "A good meal is a small act of optimism.", ["food"], 840),
            ("lena", "Ink test for a zine I've been avoiding for a year. It is happening. Slowly, but happening.", ["art"], 960),
            ("omar", "Playlist for a rainy walk: sparse drums, warm bass, nothing that asks too much of you.", ["music", "travel"], 1100),
            ("maya", "Unpopular opinion: a prototype that looks rough invites better feedback than one that looks finished.", ["design"], 1260),
            ("theo", "Spent an hour reading the diff instead of the docs. Ten out of ten, would unblock myself again.", ["gaming"], 1440),
            ("june", "Morning pages, three days running. Mostly complaints; occasionally a good sentence.", ["books"], 1620),
            ("kai", "The mountain was there, so we went. That's the whole story, and it's a good one.", ["hiking", "travel"], 1800),
            ("noor", "Cities are archives you can walk through.", ["travel", "design"], 2000),
            ("sam", "Reminder that rest is part of training, not a break from it.", ["running"], 2200),
            ("lena", "Filled a page with hands. Hands are the final boss of drawing.", ["art"], 2400),
            ("omar", "The best mixes are the ones where you can hear the room.", ["music"], 2700)
        ];

        var posts = new List<Post>(postDefinitions.Length);
        foreach (var (author, content, tagSlugs, minutesAgo) in postDefinitions)
        {
            var at = now.AddMinutes(-minutesAgo);
            var tagIds = tagSlugs.Select(slug => tags[slug].Id).ToArray();
            var post = Post.Create(
                users[author].Id,
                content,
                ContentSensitivity.General,
                at,
                tagIds: tagIds).Value;

            foreach (var slug in tagSlugs)
                tags[slug].RecordPostAdded(at);

            posts.Add(post);
        }

        db.Posts.AddRange(posts);

        // -------------------------------------------------------------- follows
        (string Follower, string Followed)[] followDefinitions =
        [
            ("maya", "theo"), ("maya", "june"), ("maya", "noor"), ("maya", "lena"),
            ("theo", "maya"), ("theo", "kai"), ("theo", "omar"),
            ("june", "maya"), ("june", "lena"), ("june", "noor"),
            ("kai", "theo"), ("kai", "noor"), ("kai", "sam"),
            ("noor", "maya"), ("noor", "june"), ("noor", "kai"),
            ("sam", "kai"), ("sam", "theo"), ("sam", "june"),
            ("lena", "maya"), ("lena", "june"), ("lena", "omar"),
            ("omar", "theo"), ("omar", "lena"), ("omar", "sam"),
            ("admin", "maya"), ("admin", "theo"), ("admin", "june")
        ];

        db.Follows.AddRange(followDefinitions.Select(pair =>
            Follow.Create(users[pair.Follower].Id, users[pair.Followed].Id, now)));

        // ---------------------------------------------------------------- likes
        (int Post, string User)[] likeDefinitions =
        [
            (0, "theo"), (0, "june"), (0, "noor"), (0, "lena"), (0, "admin"),
            (1, "maya"), (1, "kai"), (1, "omar"),
            (2, "maya"), (2, "june"), (2, "lena"), (2, "admin"),
            (3, "maya"), (3, "sam"),
            (4, "maya"), (4, "june"), (4, "kai"), (4, "lena"), (4, "omar"), (4, "admin"),
            (5, "kai"), (5, "theo"),
            (6, "maya"), (6, "june"),
            (7, "theo"), (7, "lena"),
            (8, "theo"), (8, "noor"),
            (9, "maya"), (9, "kai"), (9, "omar"),
            (10, "maya"), (10, "noor"),
            (11, "noor"), (11, "sam"),
            (12, "maya"), (12, "june"), (12, "lena"),
            (13, "kai"), (13, "theo"),
            (14, "maya"), (14, "june"),
            (15, "theo"), (15, "kai"),
            (16, "june"), (16, "noor"), (16, "lena"),
            (17, "kai"),
            (18, "maya"),
            (19, "theo"), (19, "sam"),
            (20, "maya"), (20, "kai"),
            (21, "theo"), (21, "kai"),
            (22, "maya"), (22, "june"),
            (23, "theo")
        ];

        var likes = new List<Like>(likeDefinitions.Length);
        foreach (var (postIndex, username) in likeDefinitions)
        {
            var post = posts[postIndex];
            likes.Add(Like.Create(post.Id, users[username].Id, now.AddMinutes(-postIndex)));
            post.IncrementLikeCount();
        }

        db.Likes.AddRange(likes);

        // ------------------------------------------------------------- comments
        (int Post, string Author, string Content, int MinutesAgo)[] commentDefinitions =
        [
            (0, "theo", "The 8am one is the keeper. It has that 'nothing is happening yet' feeling.", 10),
            (0, "lena", "This is the exact light I keep trying to draw and failing.", 7),
            (2, "maya", "Adding 'with weather in it' to my reading list criteria immediately.", 52),
            (2, "noor", "Try The Overstory if you haven't — the weather is basically a character.", 48),
            (3, "sam", "A good playlist is a public service.", 80),
            (4, "june", "'Negotiate with the street' is going straight into my vocabulary.", 120),
            (6, "maya", "The chair is right and it knows it.", 180),
            (7, "lena", "Please finish it. The half-finished ones are always the good ones.", 240),
            (11, "noor", "Twelve minutes well spent.", 580),
            (13, "june", "This is the most Sam sentence I have read all week.", 800),
            (19, "maya", "I want the longer version of this story.", 1750)
        ];

        var comments = new List<Comment>(commentDefinitions.Length);
        foreach (var (postIndex, author, content, minutesAgo) in commentDefinitions)
        {
            var post = posts[postIndex];
            comments.Add(Comment.Create(post.Id, users[author].Id, content, now.AddMinutes(-minutesAgo)).Value);
            post.IncrementCommentCount();
        }

        db.Comments.AddRange(comments);

        // --------------------------------------------------------- conversations
        (string A, string B, (string Sender, string Content, int MinutesAgo)[] Messages)[] conversationDefinitions =
        [
            ("maya", "theo",
            [
                ("maya", "That query rewrite is still haunting me. Did the index actually hold up?", 240),
                ("theo", "So far. p95 dropped from 180ms to 22ms. I owe the database an apology.", 235),
                ("maya", "Please put that sentence in the PR description.", 231)
            ]),
            ("june", "lena",
            [
                ("june", "The zine. How is it going?", 900),
                ("lena", "I have printed the same page six times and changed the title twice.", 894),
                ("june", "That is the correct number of times.", 890),
                ("lena", "I'll send you a scan when the ink dries.", 885)
            ]),
            ("kai", "noor",
            [
                ("kai", "Next hike: the ridge route or the lake loop?", 1500),
                ("noor", "Ridge. The lake is nicer but the ridge has that one turn where the city appears.", 1494),
                ("kai", "Sold. Saturday, early.", 1488)
            ]),
            ("sam", "omar",
            [
                ("omar", "I need a running playlist that doesn't feel like a corporate motivation seminar.", 2100),
                ("sam", "Send me a tempo and I'll trade you for a bass line.", 2095),
                ("omar", "Deal.", 2090)
            ])
        ];

        var conversations = new List<Conversation>(conversationDefinitions.Length);
        var messages = new List<Message>();
        foreach (var (a, b, conversationMessages) in conversationDefinitions)
        {
            var conversation = Conversation.CreateDirect(users[a].Id, users[b].Id, now).Value;
            conversations.Add(conversation);

            foreach (var (sender, content, minutesAgo) in conversationMessages)
            {
                var at = now.AddMinutes(-minutesAgo);
                messages.Add(Message.Create(conversation.Id, users[sender].Id, content, at).Value);
                conversation.RecordMessage(at);
            }
        }

        db.Conversations.AddRange(conversations);
        db.Messages.AddRange(messages);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Demo seed complete: {Users} users, {Tags} tags, {Posts} posts, {Follows} follows, " +
            "{Likes} likes, {Comments} comments, {Conversations} conversations. " +
            "Sign in as any demo username with password {Password}.",
            users.Count, tags.Count, posts.Count, followDefinitions.Length, likes.Count,
            comments.Count, conversations.Count, password);
    }
}
