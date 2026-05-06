namespace Domain.Entities.Abstractions;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}