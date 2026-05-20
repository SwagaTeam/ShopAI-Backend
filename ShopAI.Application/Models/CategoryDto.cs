namespace ShopAI.Application.Models
{
    public record CategoryDto(
    Guid Id,
    string Name,
    Guid ShopId,
    Guid? ParentCategoryId)
    {
        // Список вложенных подкатегорий
        public List<CategoryDto> SubCategories { get; set; } = new();
    }
}
