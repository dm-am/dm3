
namespace DM.Domain.Community.Features.Polls;

internal interface IPollFactory
{
    CreatePollEntity Create(CreatePoll createPoll);
}
