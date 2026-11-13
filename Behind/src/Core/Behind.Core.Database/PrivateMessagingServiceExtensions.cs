using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Behind.Core.Database;

public static class PrivateMessagingServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPrivateMessagingServices()
        {
            services.TryAddScoped<PrivateMessageStore>();
            services.TryAddScoped<IReportConversations>(provider => provider.GetRequiredService<PrivateMessageStore>());
            return services;
        }
    }
}
