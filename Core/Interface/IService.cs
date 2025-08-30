namespace Core.Interface;

public interface IService<TResponse>
{
    public Task<TResponse?> FetchAsync(string input, double? grams = null);
}