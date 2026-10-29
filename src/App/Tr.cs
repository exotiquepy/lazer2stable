using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Exotique.Core;

namespace Exotique.App;

/// <summary>
/// Texte traduit en XAML : <c>Text="{app:Tr SetupTitle}"</c>.
/// La liaison suit la langue choisie, l'interface change de langue sans redémarrer.
/// </summary>
public sealed class Tr : MarkupExtension
{
    public Tr()
    {
    }

    public Tr(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.I, Mode = BindingMode.OneWay };
}
