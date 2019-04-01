using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Forum.Implementation;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Fora;

namespace DM.Web.API.Services.Fora
{
    /// <inheritdoc />
    public class ForumApiService : IForumApiService
    {
        private readonly IForumService forumService;
        private readonly IMapper mapper;

        /// <inheritdoc />
        public ForumApiService(
            IForumService forumService,
            IMapper mapper)
        {
            this.forumService = forumService;
            this.mapper = mapper;
        }

        /// <inheritdoc />
        public async Task<ListEnvelope<Forum>> Get()
        {
            var fora = await forumService.GetForaList();
            return new ListEnvelope<Forum>(fora.Select(mapper.Map<Forum>));
        }

        /// <inheritdoc />
        public async Task<Envelope<Forum>> Get(string id)
        {
            var forum = await forumService.GetForum(id);
            return new Envelope<Forum>(mapper.Map<Forum>(forum));
        }
    }
}