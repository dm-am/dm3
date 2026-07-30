using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
        private async Task CreateBlogs(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Check if blogs already exist
        var existingBlogsCount = await _dbContext.Set<DbBlog>().CountAsync();
        if (existingBlogsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Blogs already exist ({existingBlogsCount}), skipping");
            return;
        }

        var mentor = users.First(u => u.Role == UserRole.Mentor);
        var experiencedUsers = users.Where(u => u.QuantityRating >= 100).ToList();
        var newbieUsers = users.Where(u => u.QuantityRating < 100).ToList();

        if (experiencedUsers.Count == 0) experiencedUsers = users.Take(2).ToList();

        // Local aliases for repeated enum values
        var (Active, Draft, Closed) = (ModuleStatus.Active, ModuleStatus.Draft, ModuleStatus.Closed);
        var (Private, Public) = (DraftVisibility.Private, DraftVisibility.Public);
        var (Approved, Awaiting) = (PremoderationStatus.Approved, PremoderationStatus.AwaitingApproval);

        // Assistants distribution: most blogs (80%) have 0, ~15% have 1, ~5% have 2
        var blogTemplates = new[]
        {
            // Active blogs (12) - spread 0-25, IsNew=true means activated < 7 days ago
            new { Title = "Заметки мастера", Description = "Советы по ведению игр и созданию сюжетов", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 25, IsNew = false, AssistantCount = 2 },
            new { Title = "Дневник приключенца", Description = "Истории из игр глазами игрока", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 20, IsNew = false, AssistantCount = 0 },
            new { Title = "Мир фэнтези", Description = "Обзоры сеттингов и игровых миров", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 16, IsNew = true, AssistantCount = 1 },
            new { Title = "Кухня мастера", Description = "Как готовить интересные сессии", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 12, IsNew = false, AssistantCount = 0 },
            new { Title = "Истории персонажей", Description = "Галерея лучших персонажей наших игр", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 10, IsNew = true, AssistantCount = 0 },
            new { Title = "Системный подход", Description = "Анализ разных игровых систем", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 8, IsNew = false, AssistantCount = 1 },
            new { Title = "Творческая мастерская", Description = "Советы по написанию постов и описаний", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 5, IsNew = false, AssistantCount = 0 },
            new { Title = "Новости ролевок", Description = "Обзоры новинок и событий в мире RPG", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 4, IsNew = true, AssistantCount = 0 },
            new { Title = "Путеводитель по системам", Description = "Сравнение и обзор игровых систем", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 3, IsNew = false, AssistantCount = 0 },
            new { Title = "Архив сессий", Description = "Записи и отчеты с прошедших игр", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 2, IsNew = false, AssistantCount = 0 },
            new { Title = "D&D для всех", Description = "Все о Dungeons & Dragons", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 1, IsNew = false, AssistantCount = 0 },
            new { Title = "Киберпанк-хроники", Description = "Блог о киберпанк-играх", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 0, IsNew = false, AssistantCount = 0 },
            // Closed blogs (3) - spread 0-8
            new { Title = "Советы новичкам", Description = "Как начать играть в ролевые игры", Status = Closed, DraftVisibility = Public, Premod = Approved, Readers = 8, IsNew = false, AssistantCount = 0 },
            new { Title = "Мастерская сюжетов", Description = "Идеи для квестов и приключений", Status = Closed, DraftVisibility = Public, Premod = Approved, Readers = 4, IsNew = false, AssistantCount = 1 },
            new { Title = "Голос игрока", Description = "Рассказы и впечатления от игр", Status = Closed, DraftVisibility = Public, Premod = Approved, Readers = 0, IsNew = false, AssistantCount = 0 },
            // Draft blogs (3): AwaitingApproval = Private (newbie), Approved = Public (experienced)
            new { Title = "Мой первый блог", Description = "Пробую писать", Status = Draft, DraftVisibility = Private, Premod = Awaiting, Readers = 0, IsNew = false, AssistantCount = 0 },
            new { Title = "Черновик идей", Description = "Заготовки для будущих статей", Status = Draft, DraftVisibility = Public, Premod = Approved, Readers = 0, IsNew = false, AssistantCount = 0 },
            new { Title = "Планы на будущее", Description = "Скоро здесь будет интересно", Status = Draft, DraftVisibility = Private, Premod = Awaiting, Readers = 0, IsNew = false, AssistantCount = 0 },
        };

        for (var bi = 0; bi < blogTemplates.Length; bi++)
        {
            var template = blogTemplates[bi];
            var isNewbieOwner = template.Premod == PremoderationStatus.AwaitingApproval;
            var owner = isNewbieOwner && newbieUsers.Count > 0
                ? newbieUsers[0]
                : experiencedUsers[bi % experiencedUsers.Count];

            // Generate realistic dates based on blog status and age
            // Draft: recent (1-14 days)
            // Active "new" (IsNew=true): created 7-14 days ago, activated 1-5 days ago
            // Active established (high readers): old (90-365 days)
            // Active newer (lower readers): medium (30-90 days)
            // Closed: old blogs that were active for some time, then closed
            DateTimeOffset blogCreatedUtc;
            DateTimeOffset? blogActivatedUtc;
            DateTimeOffset? blogClosedUtc = null;

            if (template.Status == ModuleStatus.Draft)
            {
                // Drafts are recent - work in progress
                blogCreatedUtc = now.AddDays(-_random.Next(1, 14));
                blogActivatedUtc = null;
            }
            else if (template.Status == ModuleStatus.Closed)
            {
                // Closed blogs: were active for a while, then closed 30-180 days ago
                var daysAgoCreated = _random.Next(180, 540); // Created 6-18 months ago
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                var activationDelay = _random.Next(1, 7);
                blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
                // Blog was active for 60-180 days before closing
                var activeDuration = _random.Next(60, 180);
                blogClosedUtc = blogActivatedUtc.Value.AddDays(activeDuration);
                // Make sure closedUtc is in the past (at least 30 days ago)
                if (blogClosedUtc > now.AddDays(-30))
                {
                    blogClosedUtc = now.AddDays(-_random.Next(30, 90));
                }
            }
            else if (template.IsNew)
            {
                // "New" blogs: created recently, activated within last week
                var daysAgoCreated = _random.Next(7, 14);
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                blogActivatedUtc = now.AddDays(-_random.Next(1, 5));
            }
            else if (template.Readers >= 8)
            {
                // Popular established blogs: older, been around a while
                var daysAgoCreated = _random.Next(180, 365);
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                var activationDelay = _random.Next(1, 7);
                blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
            }
            else if (template.Readers >= 4)
            {
                // Medium popularity: medium age
                var daysAgoCreated = _random.Next(60, 180);
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                var activationDelay = _random.Next(1, 5);
                blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
            }
            else
            {
                // Lower popularity: newer
                var daysAgoCreated = _random.Next(30, 90);
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                var activationDelay = _random.Next(1, 3);
                blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
            }

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
                CommentsEnabled = true,
                PublicationCount = 0,
                CommentCount = 0,
                IsRemoved = false,
                // Temporary placeholder - will be updated after SaveChanges
                PublicId = $"t{_guidFactory.Create():N}"[..10]
            };

            _dbContext.Set<DbBlog>().Add(blog);
            result.BlogsCreated++;

            // Create rubrics
            var rubrics = new[]
            {
                new Rubric { RubricId = _guidFactory.Create(), BlogId = blog.BlogId, Title = "Общее", AccessType = RubricAccessType.Open, SortOrder = 1, CreatedUtc = blog.CreatedUtc, IsArchived = false, IsRemoved = false },
                new Rubric { RubricId = _guidFactory.Create(), BlogId = blog.BlogId, Title = "Для избранных", AccessType = RubricAccessType.Private, SortOrder = 2, CreatedUtc = blog.CreatedUtc, IsArchived = false, IsRemoved = false },
            };

            foreach (var rubric in rubrics)
            {
                _dbContext.Set<Rubric>().Add(rubric);
            }

            // Skip publications for draft blogs
            if (template.Status == ModuleStatus.Draft) continue;

            // Create publications - varied templates for different blogs
            var allPublicationTemplates = new[]
            {
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
            };

            // Select 3 publications for this blog (rotating through templates)
            var publicationTemplates = allPublicationTemplates.Skip((bi * 3) % allPublicationTemplates.Length).Take(3).ToArray();
            if (publicationTemplates.Length < 3)
            {
                publicationTemplates = allPublicationTemplates.Take(3).ToArray();
            }

            // Calculate available time range for publications
            // For closed blogs: between activation and closing
            // For active blogs: between activation and now
            var blogEndDate = blogClosedUtc ?? now;
            var blogAgeInDays = blogActivatedUtc.HasValue
                ? (int)(blogEndDate - blogActivatedUtc.Value).TotalDays
                : 1;
            var pubIndex = 0;

            foreach (var (title, content) in publicationTemplates)
            {
                // Spread publications evenly across the blog's lifetime
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
                    RubricId = rubrics[0].RubricId,
                    PublicationNumber = pubIndex, // Unique within blog
                    Title = title,
                    Content = content + "\n\nЭто пример содержания публикации. Здесь может быть гораздо больше текста с форматированием.",
                    Preview = content[..Math.Min(100, content.Length)] + "...",
                    CreatedUtc = pubCreatedUtc,
                    IsPublished = true,
                    PublishedUtc = pubCreatedUtc,
                    CommentsEnabled = true,
                    ViewCount = _random.Next(10, 500),
                    CommentCount = 0,
                    IsRemoved = false
                };

                _dbContext.Set<Publication>().Add(publication);
                blog.PublicationCount++;
                result.PublicationsCreated++;

                // Add publication comments
                var pubCommentTexts = new[]
                {
                    "Отличная статья, спасибо!",
                    "Очень полезно, особенно для новичков.",
                    "Согласен с автором, все так и есть.",
                    "Интересный взгляд на тему.",
                    "Жду еще статей!",
                };

                var pubCommentsToCreate = _random.Next(2, 5);
                // For closed blogs, comments should be before closing; for active - before now
                var commentsEndDate = blogClosedUtc ?? now;
                var hoursAvailable = (int)(commentsEndDate - publication.PublishedUtc!.Value).TotalHours;
                for (var pc = 0; pc < pubCommentsToCreate; pc++)
                {
                    // Spread comments evenly within available time
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
                        CreatedUtc = publication.PublishedUtc.Value.AddHours(commentOffsetHours),
                        Text = pubCommentTexts[_random.Next(pubCommentTexts.Length)],
                        IsRemoved = false
                    };

                    _dbContext.Set<DbComment>().Add(pubComment);
                    publication.CommentCount++;
                    publication.LastCommentId = pubComment.CommentId;
                    blog.CommentCount++;
                }
            }

            // Add blog assistants (most blogs have 0, few have 1, very few have 2)
            if (template.AssistantCount > 0)
            {
                var availableForAssistant = users
                    .Where(u => u.UserId != owner.UserId)
                    .OrderBy(_ => _random.Next())
                    .Take(template.AssistantCount)
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

            // Add readers based on template
            var readerCount = Math.Min(template.Readers, users.Count - 2); // Don't exceed available users
            var readers = users.Where(u => u.UserId != owner.UserId).OrderBy(_ => _random.Next()).Take(readerCount).ToList();
            foreach (var reader in readers)
            {
                _dbContext.Set<Subscription>().Add(new Subscription
                {
                    SubscriptionId = _guidFactory.Create(),
                    SubscriberId = reader.UserId,
                    TargetType = SubscriptionTargetType.Blog,
                    TargetId = blog.BlogId,
                    Settings = SubscriptionSettings.None,
                    CreatedUtc = blog.CreatedUtc.AddDays(_random.Next(1, 14))
                });
            }

            // Batch save after each blog
            await _dbContext.SaveChangesAsync();

            // Reload the blog to get the auto-generated SerialNumber
            await _dbContext.Entry(blog).ReloadAsync();

            // Update PublicId from SerialNumber (which was auto-generated on insert)
            blog.PublicId = _publicIdService.Encode(blog.SerialNumber);
            await _dbContext.SaveChangesAsync();
        }

        result.Details.Add($"Created {result.BlogsCreated} blogs with {result.PublicationsCreated} publications");
    }
}
