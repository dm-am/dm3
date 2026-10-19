using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.WebsiteTestimonials;

public class WebsiteTestimonialApiService
{
    private readonly IWebsiteTestimonialService _testimonialService;
    private readonly WebsiteTestimonialMapper _mapper;

    public WebsiteTestimonialApiService(
        IWebsiteTestimonialService testimonialService,
        WebsiteTestimonialMapper mapper)
    {
        _testimonialService = testimonialService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<WebsiteTestimonialDto>> GetList(WebsiteTestimonialsQuery query)
    {
        var (testimonials, paging) = await _testimonialService.GetListAsync(query);
        var apiTestimonials = testimonials.Select(_mapper.ToTestimonial);
        return new ListEnvelope<WebsiteTestimonialDto>(apiTestimonials, new PagingInfo(paging));
    }

    public async Task<Envelope<WebsiteTestimonialDto>> Get(Guid id)
    {
        var testimonial = await _testimonialService.GetAsync(id);
        return new Envelope<WebsiteTestimonialDto>(_mapper.ToTestimonial(testimonial));
    }

    public async Task<Envelope<WebsiteTestimonialDto>> Create(CreateWebsiteTestimonialRequest request)
    {
        var testimonial = await _testimonialService.CreateAsync(_mapper.ToCreateTestimonial(request));
        return new Envelope<WebsiteTestimonialDto>(_mapper.ToTestimonial(testimonial));
    }

    public async Task<Envelope<WebsiteTestimonialDto>> Update(Guid id, UpdateWebsiteTestimonialRequest request)
    {
        var updateTestimonial = new UpdateWebsiteTestimonial
        {
            Id = id,
            Text = request.Text ?? string.Empty
        };
        var testimonial = await _testimonialService.UpdateAsync(updateTestimonial);
        return new Envelope<WebsiteTestimonialDto>(_mapper.ToTestimonial(testimonial));
    }

    public Task Delete(Guid id) => _testimonialService.DeleteAsync(id);
}
