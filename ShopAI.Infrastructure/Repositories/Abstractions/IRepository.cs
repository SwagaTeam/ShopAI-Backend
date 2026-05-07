using System.Linq.Expressions;
using Domain.Entities;
using Domain.Entities.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Abstractions;

public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<ICollection<T>> GetAllAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Delete(T entity);
    Task SaveAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}

public interface IShopRepository : IRepository<Shop>
{
    Task<Shop?> GetByUrlAliasAsync(string alias);
    Task<List<Shop>> GetShopsByCategoryAsync(Guid categoryId);
}

public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> GetByShopIdAsync(Guid shopId);
    Task<List<Product>> GetLatestAsync(int count);
    Task<List<Product>> GetPopularAsync(int count);
    Task<List<Product>> GetProductsByShopAndCategoryAsync(Guid shopId, Guid categoryId);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<List<Category>> GetByShopIdAsync(Guid shopId);
    Task<ICollection<Category>> GetAllWithShopsAsync(CancellationToken ct = default);
}

public interface IOrderRepository : IRepository<Order>
{
    Task<List<Order>> GetByShopIdAsync(Guid shopId);
}

public interface IBrandRepository : IRepository<Brand> 
{
    Task<Brand?> GetByNameAsync(string name);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
}

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
}

public interface ICartRepository : IRepository<Cart>
{
    Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<CartItem?> GetItemAsync(Guid cartId, Guid productId, CancellationToken ct);
    Task AddItemAsync(CartItem item);
    void UpdateItem(CartItem item);
    void RemoveItem(CartItem item);
}