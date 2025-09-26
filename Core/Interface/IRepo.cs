namespace Core.Interface;

public interface IRepo<T> where T : class
{
    public Task<List<T>> GetAll();
    public Task<T?> Get(string username);
}