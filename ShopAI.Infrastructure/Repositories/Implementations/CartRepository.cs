using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Infrastructure.Repositories.Implementations
{
    public class CartRepository(AppDbContext context) : Repository<Cart>(context), ICartRepository
    {
        /// <summary>
        /// Получить корзину пользователя со всеми вложенными товарами.
        /// </summary>
        public async Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _context.Set<Cart>()
                // Включаем айтемы корзины и данные о самих продуктах для отображения (имя, цена и т.д.)
                .Include(c => _context.Set<CartItem>().Where(ci => ci.CartId == c.Id))
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);
        }

        /// <summary>
        /// Поиск конкретной позиции в конкретной корзине.
        /// Используется для проверки: есть ли уже такой товар в корзине.
        /// </summary>
        public async Task<CartItem?> GetItemAsync(Guid cartId, Guid productId, CancellationToken ct)
        {
            return await _context.Set<CartItem>()
                .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == productId, ct);
        }

        /// <summary>
        /// Добавить новый элемент в корзину.
        /// </summary>
        public async Task AddItemAsync(CartItem item)
        {
            await _context.Set<CartItem>().AddAsync(item);
        }

        /// <summary>
        /// Обновить данные элемента (например, изменить количество).
        /// </summary>
        public void UpdateItem(CartItem item)
        {
            _context.Set<CartItem>().Update(item);
        }

        /// <summary>
        /// Удалить товар из корзины.
        /// </summary>
        public void RemoveItem(CartItem item)
        {
            _context.Set<CartItem>().Remove(item);
        }

        /// <summary>
        /// Очистить всю корзину (например, после оформления заказа).
        /// </summary>
        public async Task ClearCartAsync(Guid cartId, CancellationToken ct)
        {
            var items = await _context.Set<CartItem>()
                .Where(ci => ci.CartId == cartId)
                .ToListAsync(ct);

            _context.Set<CartItem>().RemoveRange(items);
        }
    }
}