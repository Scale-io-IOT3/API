namespace Core.Interface;

public interface IClient<T> where T : IResponse
{
    public Task<T?> Fetch(string url);
}