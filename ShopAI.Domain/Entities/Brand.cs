using Domain.Entities.Abstractions;

public class Brand : Entity
{
    protected Brand() { }

    public Brand(string name, string logoUrl)
    {
        Name = name;
        LogoUrl = logoUrl;
    }

    public string Name { get; set; }
    public string LogoUrl { get; set; }
}