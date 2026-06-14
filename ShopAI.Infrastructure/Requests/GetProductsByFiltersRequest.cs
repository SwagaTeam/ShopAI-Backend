namespace ShopAI.Infrastructure.Requests;

/// <summary>
/// Query-параметры для фильтрации, сортировки и пагинации товаров.
/// </summary>
public class GetProductsByFiltersRequest
{
    /// <summary>Фильтр по идентификатору магазина.</summary>
    public Guid? ShopId { get; set; }

    /// <summary>Фильтр по идентификатору категории.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Фильтр по глобальной категории сайта.</summary>
    public Guid? GlobalCategoryId { get; set; }

    /// <summary>Фильтр по идентификатору бренда.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Строка быстрого поиска по названию товара через PostgreSQL pg_trgm.</summary>
    public string? SearchTerm { get; set; }

    /// <summary>Минимальная цена товара.</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>Максимальная цена товара.</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Минимальный остаток товара на складе.</summary>
    public int? MinStock { get; set; }

    /// <summary>Максимальный остаток товара на складе.</summary>
    public int? MaxStock { get; set; }

    /// <summary>Теги для фильтрации. Обычно передаются строкой через запятую.</summary>
    public string? Tags { get; set; }

    /// <summary>Если true, возвращает только товары с положительным остатком.</summary>
    public bool? InStock { get; set; }

    /// <summary>Минимальный средний рейтинг товара.</summary>
    public double? MinRating { get; set; }

    /// <summary>Поле сортировки: relevance, price, name, rating, stock или createdAt. relevance работает вместе с SearchTerm.</summary>
    public string? SortBy { get; set; } // "price", "name", "rating", "createdAt"

    /// <summary>Если true, сортировка выполняется по убыванию.</summary>
    public bool SortDescending { get; set; } = false;

    /// <summary>Номер страницы, начиная с 1.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>Количество товаров на странице.</summary>
    public int PageSize { get; set; } = 10;
}
