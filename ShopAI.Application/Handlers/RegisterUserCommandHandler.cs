using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

/// <summary>
/// Запрос на регистрацию нового пользователя.
/// </summary>
/// <param name="FullName">Полное имя пользователя.</param>
/// <param name="Email">Email пользователя. Используется для входа и должен быть уникальным.</param>
/// <param name="Phone">Контактный телефон пользователя.</param>
/// <param name="Password">Пароль пользователя в открытом виде; на сервере сохраняется только хеш.</param>
public record RegisterUserCommand(string FullName, string Email, string Phone, string Password) : IRequest<Guid>;

public class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IConfiguration configuration)
    : IRequestHandler<RegisterUserCommand, Guid>
{
    public async Task<Guid> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var emailExists = await userRepository.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);
        if (emailExists)
        {
            throw new InvalidOperationException("Пользователь с таким email уже зарегистрирован.");
        }

        var (hash, salt) = passwordHasher.Hash(request.Password);
        var role = IsConfiguredAdminEmail(request.Email) ? User.AdminRole : User.UserRole;

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email.ToLower(),
            Phone = request.Phone,
            Password = hash,
            Salt = salt,
            Role = role
        };

        await userRepository.AddAsync(user);
        await userRepository.SaveAsync(ct);

        return user.Id;
    }

    private bool IsConfiguredAdminEmail(string email)
    {
        var adminEmail = configuration["Admin:Email"];
        return !string.IsNullOrWhiteSpace(adminEmail)
               && string.Equals(email.Trim(), adminEmail.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
