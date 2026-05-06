using Domain.Entities;
using MediatR;
using ShopAI.Application.Helpers.Abstractions;
using ShopAI.Infrastructure.Repositories.Abstractions;

namespace ShopAI.Application.Handlers;

public record RegisterUserCommand(string FullName, string Email, string Phone, string Password) : IRequest<Guid>;

public class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
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

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email.ToLower(),
            Phone = request.Phone,
            Password = hash,
            Salt = salt
        };

        await userRepository.AddAsync(user);
        await userRepository.SaveAsync(ct);

        return user.Id;
    }
}