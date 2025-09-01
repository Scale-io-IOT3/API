using Core.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public class ServiceFactory(IServiceProvider provider)
{
    public T GetService<T>() where T : IService
    {
        return provider.GetRequiredService<T>();
    }
}
