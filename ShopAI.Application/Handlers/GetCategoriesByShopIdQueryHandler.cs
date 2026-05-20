using AutoMapper;
using MediatR;
using ShopAI.Application.Models;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers
{
    public record GetCategoriesByShopIdQuery(Guid ShopId) : IRequest<List<CategoryDto>>;

    public class GetCategoriesByShopIdQueryHandler(
        ICategoryRepository categoryRepository,
        IMapper mapper) : IRequestHandler<GetCategoriesByShopIdQuery, List<CategoryDto>>
    {
        public async Task<List<CategoryDto>> Handle(GetCategoriesByShopIdQuery request, CancellationToken ct)
        {
            // 1. Получаем плоский список из базы (1 SQL запрос)
            var categories = await categoryRepository.GetByShopIdAsync(request.ShopId);

            // 2. Мапим в плоский список DTO
            var flatDtos = mapper.Map<List<CategoryDto>>(categories);

            // 3. Создаем словарь для быстрого поиска по Id
            var lookup = flatDtos.ToDictionary(c => c.Id);

            // 4. Итоговый список для корневых категорий
            var tree = new List<CategoryDto>();

            // 5. Собираем дерево
            foreach (var dto in flatDtos)
            {
                // Если у категории есть родитель и этот родитель найден в словаре
                if (dto.ParentCategoryId.HasValue && lookup.TryGetValue(dto.ParentCategoryId.Value, out var parent))
                {
                    // Добавляем текущую категорию в SubCategories родителя
                    parent.SubCategories.Add(dto);
                }
                else
                {
                    // Если родителя нет (или он не найден) — это корневая категория
                    tree.Add(dto);
                }
            }

            // Возвращаем только корневые категории (все остальные уже лежат у них внутри)
            return tree;
        }
    }
}
