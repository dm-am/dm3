using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.WebsiteTestimonials;

/// <inheritdoc />
internal class WebsiteTestimonialApiService : IWebsiteTestimonialApiService
{
    private readonly IWebsiteTestimonialService _testimonialService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public WebsiteTestimonialApiService(
        IWebsiteTestimonialService testimonialService,
        IMapper mapper)
    {
        _testimonialService = testimonialService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<WebsiteTestimonialDto>> GetList(WebsiteTestimonialsQuery query)
    {
        var (testimonials, paging) = await _testimonialService.GetListAsync(query);
        var apiTestimonials = testimonials.Select(_mapper.Map<WebsiteTestimonialDto>);
        return new ListEnvelope<WebsiteTestimonialDto>(apiTestimonials, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<WebsiteTestimonialDto>> Get(Guid id)
    {
        var testimonial = await _testimonialService.GetAsync(id);
        return new Envelope<WebsiteTestimonialDto>(_mapper.Map<WebsiteTestimonialDto>(testimonial));
    }

    /// <inheritdoc />
    public async Task<Envelope<WebsiteTestimonialDto>> Create(CreateWebsiteTestimonialRequest request)
    {
        var createTestimonial = new CreateWebsiteTestimonial
        {
            AuthorUsername = request.AuthorUsername,
            Text = request.Text
        };
        var testimonial = await _testimonialService.CreateAsync(createTestimonial);
        return new Envelope<WebsiteTestimonialDto>(_mapper.Map<WebsiteTestimonialDto>(testimonial));
    }

    /// <inheritdoc />
    public async Task<Envelope<WebsiteTestimonialDto>> Update(Guid id, UpdateWebsiteTestimonialRequest request)
    {
        var updateTestimonial = new UpdateWebsiteTestimonial
        {
            Id = id,
            Text = request.Text ?? string.Empty
        };
        var testimonial = await _testimonialService.UpdateAsync(updateTestimonial);
        return new Envelope<WebsiteTestimonialDto>(_mapper.Map<WebsiteTestimonialDto>(testimonial));
    }

    /// <inheritdoc />
    public Task Delete(Guid id) => _testimonialService.DeleteAsync(id);
}
