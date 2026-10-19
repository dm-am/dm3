using DM.Infrastructure.Core.Extensions;

namespace DM.Workers.NotificationDispatcher;

class Program
{
    static void Main(string[] args) => WorkerHost.Run<Startup>(args);
}
