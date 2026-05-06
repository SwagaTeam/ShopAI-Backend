using Domain.Entities.Abstractions;

namespace Domain.Entities;

public class Brand(string name, string logoUrl) : Entity
{
    public string Name { get; private set; } = name;
    public string LogoUrl { get; private set; } = logoUrl;
}