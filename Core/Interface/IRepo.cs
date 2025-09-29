namespace Core.Interface;

public interface IRepo<T> where T : class
{
    public Task<List<T>> GetAll();
    public Task<T?> FindByUsername(string username);
    public Task<T?> FindById(int id);
    public Task CreateOrUpdate(T entity);
}