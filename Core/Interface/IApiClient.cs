namespace Core.Interface;

public interface IApiClient<T> where T : ISourceResponse
{
    public Task<T?> Fetch(string input);
}