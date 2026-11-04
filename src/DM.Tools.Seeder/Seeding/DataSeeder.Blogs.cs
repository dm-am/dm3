using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Entities.Blog;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>A blog of the seed as it is described before it is written.</summary>
    private sealed record BlogTemplate
    {
        public required string Title { get; init; }

        public required string Description { get; init; }

        public required ModuleStatus Status { get; init; }

        public DraftVisibility DraftVisibility { get; init; } = DraftVisibility.Public;

        public PremoderationStatus Premod { get; init; } = PremoderationStatus.Approved;

        public required int Readers { get; init; }

        public bool IsNew { get; init; }

        public int AssistantCount { get; init; }
    }

    /// <summary>The blogs of the seed, in the order they are written.</summary>
    private static BlogTemplate[] BlogTemplates()
    {
        var (Active, Draft, Closed) = (ModuleStatus.Active, ModuleStatus.Draft, ModuleStatus.Closed);
        var (Private, Awaiting) = (DraftVisibility.Private, PremoderationStatus.AwaitingApproval);

        return
        [
            // Active: IsNew means activated less than a week ago
            new() { Title = "Заметки мастера", Description = "Советы по ведению игр и созданию сюжетов", Status = Active, Readers = 25, AssistantCount = 2 },
            new() { Title = "Дневник приключенца", Description = "Истории из игр глазами игрока", Status = Active, Readers = 20 },
            new() { Title = "Мир фэнтези", Description = "Обзоры сеттингов и игровых миров", Status = Active, Readers = 16, IsNew = true, AssistantCount = 1 },
            new() { Title = "Кухня мастера", Description = "Как готовить интересные сессии", Status = Active, Readers = 12 },
            new() { Title = "Истории персонажей", Description = "Галерея лучших персонажей наших игр", Status = Active, Readers = 10, IsNew = true },
            new() { Title = "Системный подход", Description = "Анализ разных игровых систем", Status = Active, Readers = 8, AssistantCount = 1 },
            new() { Title = "Творческая мастерская", Description = "Советы по написанию постов и описаний", Status = Active, Readers = 5 },
            new() { Title = "Новости ролевок", Description = "Обзоры новинок и событий в мире RPG", Status = Active, Readers = 4, IsNew = true },
            new() { Title = "Путеводитель по системам", Description = "Сравнение и обзор игровых систем", Status = Active, Readers = 3 },
            new() { Title = "Архив сессий", Description = "Записи и отчеты с прошедших игр", Status = Active, Readers = 2 },
            new() { Title = "D&D для всех", Description = "Все о Dungeons & Dragons", Status = Active, Readers = 1 },
            new() { Title = "Киберпанк-хроники", Description = "Блог о киберпанк-играх", Status = Active, Readers = 0 },
            // Closed
            new() { Title = "Советы новичкам", Description = "Как начать играть в ролевые игры", Status = Closed, Readers = 8 },
            new() { Title = "Мастерская сюжетов", Description = "Идеи для квестов и приключений", Status = Closed, Readers = 4, AssistantCount = 1 },
            new() { Title = "Голос игрока", Description = "Рассказы и впечатления от игр", Status = Closed, Readers = 0 },
            // Drafts: AwaitingApproval is private (newbie), Approved public (experienced)
            new() { Title = "Мой первый блог", Description = "Пробую писать", Status = Draft, DraftVisibility = Private, Premod = Awaiting, Readers = 0 },
            new() { Title = "Черновик идей", Description = "Заготовки для будущих статей", Status = Draft, Readers = 0 },
            new() { Title = "Планы на будущее", Description = "Скоро здесь будет интересно", Status = Draft, DraftVisibility = Private, Premod = Awaiting, Readers = 0 },
        ];
    }

    /// <summary>
    /// The publications a blog draws three of, rotating through the list by the
    /// place of the blog.
    /// </summary>
    private static readonly (string Title, string Content)[] PublicationTemplates =
    [
        ("Как начать свою первую игру", "В этой статье я расскажу о подготовке к первой сессии. Главное — не бояться ошибок!"),
        ("Топ-5 ошибок начинающих мастеров", "Разбираем типичные ошибки и способы их избежать."),
        ("Создание интересных NPC", "Как сделать неигровых персонажей запоминающимися."),
        ("Секреты атмосферы в играх", "Как создать погружение для игроков с помощью описаний и музыки."),
        ("Баланс боя и ролеплея", "Как найти золотую середину между боевкой и отыгрышем."),
        ("Работа со сложными игроками", "Советы по разрешению конфликтов за игровым столом."),
        ("Импровизация для мастера", "Когда план рушится — как выкрутиться и сделать сессию интересной."),
        ("Обзор системы D&D 5e", "Плюсы и минусы самой популярной системы для начинающих."),
        ("Построение сюжетной арки", "Как спланировать кампанию от начала до эпичного финала."),
        ("Персонаж как часть истории", "Как интегрировать бэкграунд персонажей в общий сюжет."),
        ("Музыка для ролевых игр", "Подборка саундтреков для разных жанров и ситуаций."),
        ("Мои любимые монстры", "Обзор интересных противников и как их правильно использовать."),
    ];

    private static readonly string[] PublicationCommentTexts =
    [
        "Отличная статья, спасибо!",
        "Очень полезно, особенно для новичков.",
        "Согласен с автором, все так и есть.",
        "Интересный взгляд на тему.",
        "Жду еще статей!",
    ];

    private async Task CreateBlogs(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var existingBlogsCount = await _dbContext.Set<DbBlog>().CountAsync();
        if (existingBlogsCount > 0)
        {
            result.Details.Add($"Blogs already exist ({existingBlogsCount}), skipping");
            return;
        }

        var mentor = users.First(u => u.Role == UserRole.Mentor);
        var experiencedUsers = users.Where(u => !ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();
        var newbieUsers = users.Where(u => ProbationPolicy.IsNewbie(u.QuantityRating)).ToList();

        if (experiencedUsers.Count == 0) experiencedUsers = users.Take(2).ToList();

        var blogTemplates = BlogTemplates();

        for (var bi = 0; bi < blogTemplates.Length; bi++)
        {
            var template = blogTemplates[bi];
            var isNewbieOwner = template.Premod == PremoderationStatus.AwaitingApproval;
            var owner = isNewbieOwner && newbieUsers.Count > 0
                ? newbieUsers[0]
                : experiencedUsers[bi % experiencedUsers.Count];

            var (blogCreatedUtc, blogActivatedUtc, blogClosedUtc) = BlogDates(template);

            // The readable address comes from the sequence before the insert, as in the repository (SerialNumberAllocator).
            var blogSerialNumber = await SerialNumberAllocator.NextAsync<DbBlog>(_dbContext);

            var blog = new DbBlog
            {
                BlogId = _guidFactory.Create(),
                AuthorId = owner.UserId,
                Title = template.Title,
                Description = template.Description,
                CreatedUtc = blogCreatedUtc,
                Status = template.Status,
                ActivatedUtc = blogActivatedUtc,
                ClosedUtc = blogClosedUtc,
                PremoderationStatus = template.Premod,
                MentorId = isNewbieOwner ? mentor.UserId : null,
                DraftVisibility = template.DraftVisibility,
                SerialNumber = blogSerialNumber,
                PublicId = _publicIdService.Encode(blogSerialNumber)
            };

            _dbContext.Set<DbBlog>().Add(blog);
            result.BlogsCreated++;

            var rubrics = SeedRubrics(blog);

            if (template.Status == ModuleStatus.Draft) continue;

            SeedPublications(blog, owner, rubrics[0], bi, users, result);
            SeedBlogAssistants(blog, owner, template.AssistantCount, users);
            SeedBlogReaders(blog, owner, template.Readers, users);

            await _dbContext.SaveChangesAsync();
        }

        result.Details.Add($"Created {result.BlogsCreated} blogs with {result.PublicationsCreated} publications");
    }

    /// <summary>
    /// The dates of a blog, by its status and its age.
    /// </summary>
    private (DateTimeOffset Created, DateTimeOffset? Activated, DateTimeOffset? Closed) BlogDates(
        BlogTemplate template)
    {
        DateTimeOffset blogCreatedUtc;
        DateTimeOffset? blogActivatedUtc;
        DateTimeOffset? blogClosedUtc = null;

        if (template.Status == ModuleStatus.Draft)
        {
            // Drafts are recent - work in progress
            blogCreatedUtc = _now.AddDays(-_random.Next(1, 14));
            blogActivatedUtc = null;
        }
        else if (template.Status == ModuleStatus.Closed)
        {
            // Closed blogs: were active for a while, then closed 30-180 days ago
            var daysAgoCreated = _random.Next(180, 540); // Created 6-18 months ago
            blogCreatedUtc = _now.AddDays(-daysAgoCreated);
            var activationDelay = _random.Next(1, 7);
            blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
            var activeDuration = _random.Next(60, 180);
            blogClosedUtc = blogActivatedUtc.Value.AddDays(activeDuration);
            if (blogClosedUtc > _now.AddDays(-30))
            {
                blogClosedUtc = _now.AddDays(-_random.Next(30, 90));
            }
        }
        else if (template.IsNew)
        {
            // "New" blogs: created recently, activated within last week
            var daysAgoCreated = _random.Next(7, 14);
            blogCreatedUtc = _now.AddDays(-daysAgoCreated);
            blogActivatedUtc = _now.AddDays(-_random.Next(1, 5));
        }
        else if (template.Readers >= 8)
        {
            // Popular established blogs: older, been around a while
            var daysAgoCreated = _random.Next(180, 365);
            blogCreatedUtc = _now.AddDays(-daysAgoCreated);
            var activationDelay = _random.Next(1, 7);
            blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
        }
        else if (template.Readers >= 4)
        {
            // Medium popularity: medium age
            var daysAgoCreated = _random.Next(60, 180);
            blogCreatedUtc = _now.AddDays(-daysAgoCreated);
            var activationDelay = _random.Next(1, 5);
            blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
        }
        else
        {
            // Lower popularity: newer
            var daysAgoCreated = _random.Next(30, 90);
            blogCreatedUtc = _now.AddDays(-daysAgoCreated);
            var activationDelay = _random.Next(1, 3);
            blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
        }

        return (blogCreatedUtc, blogActivatedUtc, blogClosedUtc);
    }

    /// <summary>The preview of a publication: its first hundred characters.</summary>
    private static string PreviewOf(string content) => content[..Math.Min(100, content.Length)] + "...";

    /// <summary>The two rubrics every blog of the seed has.</summary>
    private Rubric[] SeedRubrics(DbBlog blog)
    {
        var rubrics = new[]
        {
            new Rubric { RubricId = _guidFactory.Create(), BlogId = blog.BlogId, Title = "Общее", SortOrder = 1, CreatedUtc = blog.CreatedUtc, IsArchived = false },
            new Rubric { RubricId = _guidFactory.Create(), BlogId = blog.BlogId, Title = "Для избранных", SortOrder = 2, CreatedUtc = blog.CreatedUtc, IsArchived = false },
        };

        foreach (var rubric in rubrics)
        {
            _dbContext.Set<Rubric>().Add(rubric);
        }

        return rubrics;
    }

    /// <summary>
    /// Three publications of a blog, spread evenly across its lifetime, each
    /// with its comments.
    /// </summary>
    private void SeedPublications(
        DbBlog blog, DbUser owner, Rubric rubric, int bi, List<DbUser> users,
        ComprehensiveSeedResult result)
    {
        var blogActivatedUtc = blog.ActivatedUtc;
        var blogClosedUtc = blog.ClosedUtc;

        var publicationTemplates = PublicationTemplates.Skip((bi * 3) % PublicationTemplates.Length).Take(3).ToArray();
        if (publicationTemplates.Length < 3)
        {
            publicationTemplates = PublicationTemplates.Take(3).ToArray();
        }

        var blogEndDate = blogClosedUtc ?? _now;
        var blogAgeInDays = blogActivatedUtc.HasValue
            ? (int)(blogEndDate - blogActivatedUtc.Value).TotalDays
            : 1;
        var pubIndex = 0;

        foreach (var (title, content) in publicationTemplates)
        {
            var pubOffsetDays = blogAgeInDays > 3
                ? (pubIndex * blogAgeInDays / publicationTemplates.Length) + _random.Next(1, Math.Max(2, blogAgeInDays / publicationTemplates.Length))
                : pubIndex + 1;
            pubOffsetDays = Math.Min(pubOffsetDays, Math.Max(1, blogAgeInDays - 1));
            var pubCreatedUtc = (blogActivatedUtc ?? blog.CreatedUtc).AddDays(pubOffsetDays);
            pubIndex++;

            var publication = new Publication
            {
                PublicationId = _guidFactory.Create(),
                BlogId = blog.BlogId,
                AuthorId = owner.UserId,
                RubricId = rubric.RubricId,
                PublicationNumber = pubIndex, // Unique within blog
                Title = title,
                Content = content + "\n\nПодробности и примеры в комментариях, спрашивайте.",
                Preview = PreviewOf(content),
                CreatedUtc = pubCreatedUtc,
                ViewCount = _random.Next(10, 500),
            };

            _dbContext.Set<Publication>().Add(publication);
            blog.PublicationCount++;
            result.PublicationsCreated++;

            SeedPublicationComments(blog, publication, users);
        }
    }

    /// <summary>
    /// The comments of a publication, spread evenly between its publication and
    /// the closing of the blog, or now.
    /// </summary>
    private void SeedPublicationComments(DbBlog blog, Publication publication, List<DbUser> users)
    {
        var blogClosedUtc = blog.ClosedUtc;

        var pubCommentsToCreate = _random.Next(2, 5);
        var commentsEndDate = blogClosedUtc ?? _now;
        var hoursAvailable = (int)(commentsEndDate - publication.CreatedUtc).TotalHours;
        for (var pc = 0; pc < pubCommentsToCreate; pc++)
        {
            var commentOffsetHours = hoursAvailable > pubCommentsToCreate
                ? (pc * hoursAvailable / pubCommentsToCreate) + _random.Next(1, Math.Max(2, hoursAvailable / pubCommentsToCreate))
                : pc + 1;
            commentOffsetHours = Math.Min(commentOffsetHours, Math.Max(1, hoursAvailable - 1));

            var commentAuthor = users[_random.Next(users.Count)];
            var pubComment = new DbComment
            {
                CommentId = _guidFactory.Create(),
                EntityId = publication.PublicationId,
                AuthorId = commentAuthor.UserId,
                CreatedUtc = publication.CreatedUtc.AddHours(commentOffsetHours),
                Text = PublicationCommentTexts[_random.Next(PublicationCommentTexts.Length)],
            };

            _dbContext.Set<DbComment>().Add(pubComment);
            publication.CommentCount++;
            publication.LastCommentId = pubComment.CommentId;
            blog.CommentCount++;
        }
    }

    /// <summary>
    /// The assistants of a blog: most blogs have none, a few one, very few two.
    /// </summary>
    private void SeedBlogAssistants(DbBlog blog, DbUser owner, int assistantCount, List<DbUser> users)
    {
        if (assistantCount > 0)
        {
            var availableForAssistant = users
                .Where(u => u.UserId != owner.UserId)
                .OrderBy(_ => _random.Next())
                .Take(assistantCount)
                .ToList();

            for (var ai = 0; ai < availableForAssistant.Count; ai++)
            {
                _dbContext.Set<BlogAssistant>().Add(new BlogAssistant
                {
                    BlogAssistantId = _guidFactory.Create(),
                    BlogId = blog.BlogId,
                    UserId = availableForAssistant[ai].UserId,
                    JoinedUtc = blog.CreatedUtc.AddDays(_random.Next(3, 30))
                });
            }
        }
    }

    /// <summary>The readers of a blog, as many as its template asks for and the users allow.</summary>
    private void SeedBlogReaders(DbBlog blog, DbUser owner, int wanted, List<DbUser> users)
    {
        var readerCount = Math.Min(wanted, users.Count - 2); // Don't exceed available users
        var readers = users.Where(u => u.UserId != owner.UserId).OrderBy(_ => _random.Next()).Take(readerCount).ToList();
        AddReaders(readers, SubscriptionTargetType.Blog, blog.BlogId, blog.CreatedUtc);
    }
}
