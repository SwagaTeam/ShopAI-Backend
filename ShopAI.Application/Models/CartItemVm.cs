namespace ShopAI.Application.Models
{
    /// <summary>
    /// Модель элемента корзины для отображения.
    /// </summary>
    public record CartItemVm(Guid ProductId, string ProductName, int Quantity, decimal Price, string ImageUrl);
}
