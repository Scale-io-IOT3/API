using Core.Interface;
using Core.Interface.Login;
using Core.Interface.Meals;
using Infrastructure.Services.Login;
using Infrastructure.Services.Meals;
using Infrastructure.Utils;
using Microsoft.Extensions.DependencyInjection;
using TokenHandler = Infrastructure.Utils.TokenHandler;

namespace Infrastructure;

public static partial class DependencyInjection
{
    private static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IAuth, Authenticator>();
        services.AddScoped<ITokenHandler, TokenHandler>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMealsService, MealService>();
    }
}
