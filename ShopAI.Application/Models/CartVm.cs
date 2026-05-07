namespace ShopAI.Application.Models
{
    /// <summary>
    /// Модель всей корзины.
    /// </summary>
    public record CartVm(Guid Id, List<CartItemVm> Items, decimal TotalPrice);
}
