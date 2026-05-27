namespace ShopAI.Application.Helpers.Abstractions
{
    public interface IUserContext
    {
        Guid UserId { get; }
        string? Role { get; }
        bool IsAdmin { get; }
    }
}
