using Microsoft.Xna.Framework.Input;

namespace LightEngine.Core.Input;

/// <summary>
/// Eksen (Axis) tabanlı girişler (örneğin yatay/dikey hareket) için tanım sınıfı.
/// Pozitif ve negatif tuşları/butonları -1.0 ile +1.0 arasındaki bir değere çevirir.
/// </summary>
public class InputAxis
{
    public string Name { get; }
    public Keys PositiveKey { get; set; } = Keys.None;
    public Keys NegativeKey { get; set; } = Keys.None;
    public Buttons PositiveButton { get; set; }
    public Buttons NegativeButton { get; set; }
    public bool UseThumbstickX { get; set; }
    public bool UseThumbstickY { get; set; }

    public InputAxis(string name)
    {
        Name = name;
    }

    public InputAxis SetKeys(Keys positive, Keys negative)
    {
        PositiveKey = positive;
        NegativeKey = negative;
        return this;
    }

    public InputAxis SetButtons(Buttons positive, Buttons negative)
    {
        PositiveButton = positive;
        NegativeButton = negative;
        return this;
    }
}
