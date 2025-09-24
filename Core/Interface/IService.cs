namespace Core.Interface;

public interface IService<T> where T : IResponse
{
    Task<T?> FetchAsync();
}