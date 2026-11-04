using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbWebsiteTestimonial = DM.Infrastructure.Persistence.Entities.Community.WebsiteTestimonial;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class WebsiteTestimonialRepository : IWebsiteTestimonialRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public WebsiteTestimonialRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    // READ

    /// <inheritdoc />
    public Task<int> Count(WebsiteTestimonialsQuery query)
    {
        var dbQuery = _dbContext.WebsiteTestimonials
            .Where(t => !t.IsRemoved);

        dbQuery = ApplyFilters(dbQuery, query);

        return dbQuery.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WebsiteTestimonial>> Get(WebsiteTestimonialsQuery query, PagingData pagingData)
    {
        var dbQuery = _dbContext.WebsiteTestimonials
            .Where(t => !t.IsRemoved);

        dbQuery = ApplyFilters(dbQuery, query);

        var orderedQuery = ApplySorting(dbQuery, query);

        return await orderedQuery
            .Page(pagingData)
            .ProjectToWebsiteTestimonial()
            .ToArrayAsync();
    }

    private static IQueryable<DbWebsiteTestimonial> ApplyFilters(
        IQueryable<DbWebsiteTestimonial> query,
        WebsiteTestimonialsQuery testimonialsQuery)
    {
        // Search by text content or author username, "%" and "_" as themselves
        if (!string.IsNullOrWhiteSpace(testimonialsQuery.Search))
        {
            var pattern = LikePatterns.Contains(testimonialsQuery.Search);
            query = query.Where(t =>
                EF.Functions.ILike(t.Text, pattern, "\\") ||
                EF.Functions.ILike(t.Author.Username, pattern, "\\"));
        }

        return query;
    }

    private static IOrderedQueryable<DbWebsiteTestimonial> ApplySorting(
        IQueryable<DbWebsiteTestimonial> query,
        WebsiteTestimonialsQuery testimonialsQuery)
    {
        var sortBy = testimonialsQuery.SortBy?.ToLowerInvariant() ?? "created";
        var isDescending = string.Equals(testimonialsQuery.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        // Default is desc for created, asc for author
        if (string.IsNullOrEmpty(testimonialsQuery.SortOrder))
        {
            isDescending = sortBy == "created";
        }

        return sortBy == "author"
            ? query.OrderByDirection(t => t.Author.Username, !isDescending)
            : query.OrderByDirection(t => t.CreatedUtc, !isDescending);
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial?> Get(Guid id)
    {
        return await _dbContext.WebsiteTestimonials
            .Where(t => t.WebsiteTestimonialId == id && !t.IsRemoved)
            .ProjectToWebsiteTestimonial()
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial?> GetByAuthor(Guid authorId)
    {
        return await _dbContext.WebsiteTestimonials
            .Where(t => t.AuthorId == authorId && !t.IsRemoved)
            .ProjectToWebsiteTestimonial()
            .FirstOrDefaultAsync();
    }

    // WRITE

    /// <inheritdoc />
    public async Task<WebsiteTestimonial> Create(CreateWebsiteTestimonialEntity testimonial)
    {
        var dbTestimonial = new DbWebsiteTestimonial
        {
            WebsiteTestimonialId = testimonial.Id,
            AuthorId = testimonial.AuthorId,
            CreatedUtc = testimonial.CreatedUtc,
            Text = testimonial.Text,
            IsRemoved = false
        };

        _dbContext.WebsiteTestimonials.Add(dbTestimonial);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            // The caller pre-checks for an existing testimonial; this is the race
            // where two concurrent requests both pass that check. Translated here
            // so the domain does not have to know the storage engine's error codes.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate website testimonial", ex);
        }

        return await _dbContext.WebsiteTestimonials
            .Where(t => t.WebsiteTestimonialId == dbTestimonial.WebsiteTestimonialId)
            .ProjectToWebsiteTestimonial()
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial> Update(UpdateWebsiteTestimonialEntity testimonial)
    {
        var dbTestimonial = await _dbContext.WebsiteTestimonials
            .FirstOrDefaultAsync(t => t.WebsiteTestimonialId == testimonial.Id);

        if (dbTestimonial == null)
        {
            throw new InvalidOperationException($"Website testimonial {testimonial.Id} not found");
        }

        dbTestimonial.Text = testimonial.Text;
        dbTestimonial.ModifiedUtc = testimonial.ModifiedUtc;
        dbTestimonial.ModifiedByUserId = testimonial.ModifiedByUserId;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.WebsiteTestimonials
            .Where(t => t.WebsiteTestimonialId == testimonial.Id)
            .ProjectToWebsiteTestimonial()
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid id, Guid deletedByUserId)
    {
        var dbTestimonial = await _dbContext.WebsiteTestimonials
            .FirstOrDefaultAsync(t => t.WebsiteTestimonialId == id);

        if (dbTestimonial == null)
        {
            throw new InvalidOperationException($"Website testimonial {id} not found");
        }

        SoftDelete.Mark(dbTestimonial, deletedByUserId, _dateTimeProvider.Now);
        await _dbContext.SaveChangesAsync();
    }
}
