namespace Domain.ValueObjects;

public record ShopTheme(string CssVariables, string ConfigJson)
{
    public static ShopTheme Default() => new(":root { --primary-color: #000; }", "{}");
}