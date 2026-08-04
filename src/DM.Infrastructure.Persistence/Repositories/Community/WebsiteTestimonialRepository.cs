using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbWebsiteTestimonial = DM.Infrastructure.Persistence.Entities.Community.WebsiteTestimonial;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class WebsiteTestimonialRepository : IWebsiteTestimonialRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public WebsiteTestimonialRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _dateTimeProvider = dateTimeProvider;
    }

    // READ

    /// <inheritdoc />
    public Task<long> Count(WebsiteTestimonialsQuery query)
    {
        var dbQuery = _dbContext.WebsiteTestimonials
            .Where(t => !t.IsRemoved);

        dbQuery = ApplyFilters(dbQuery, query);

        return dbQuery.LongCountAsync();
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
            .ProjectTo<WebsiteTestimonial>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    private static IQueryable<DbWebsiteTestimonial> ApplyFilters(
        IQueryable<DbWebsiteTestimonial> query,
        WebsiteTestimonialsQuery testimonialsQuery)
    {
        // Search by text content or author username
        if (!string.IsNullOrWhiteSpace(testimonialsQuery.Search))
        {
            var searchLower = testimonialsQuery.Search.ToLowerInvariant();
            query = query.Where(t =>
                t.Text.ToLower().Contains(searchLower) ||
                (t.Author != null && t.Author.Username.ToLower().Contains(searchLower)));
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

        return sortBy switch
        {
            "author" => isDescending
                ? query.OrderByDescending(t => t.Author != null ? t.Author.Username : "")
                : query.OrderBy(t => t.Author != null ? t.Author.Username : ""),
            _ => isDescending // "created" or default
                ? query.OrderByDescending(t => t.CreatedUtc)
                : query.OrderBy(t => t.CreatedUtc)
        };
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial?> Get(Guid id)
    {
        return await _dbContext.WebsiteTestimonials
            .Where(t => t.WebsiteTestimonialId == id && !t.IsRemoved)
            .ProjectTo<WebsiteTestimonial>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial?> GetByAuthor(Guid authorId)
    {
        return await _dbContext.WebsiteTestimonials
            .Where(t => t.AuthorId == authorId && !t.IsRemoved)
            .ProjectTo<WebsiteTestimonial>(_mapper.ConfigurationProvider)
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
        await _dbContext.SaveChangesAsync();

        return await _dbContext.WebsiteTestimonials
            .Where(t => t.WebsiteTestimonialId == dbTestimonial.WebsiteTestimonialId)
            .ProjectTo<WebsiteTestimonial>(_mapper.ConfigurationProvider)
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
            .ProjectTo<WebsiteTestimonial>(_mapper.ConfigurationProvider)
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
