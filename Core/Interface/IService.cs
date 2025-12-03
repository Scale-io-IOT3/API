namespace Core.Interface;

public interface IService<T, U> where T : IResponse where U : IRequest;