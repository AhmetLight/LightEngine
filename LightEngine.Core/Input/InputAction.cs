using Microsoft.Xna.Framework.Input;

namespace LightEngine.Core.Input;

/// <summary>
/// Tuş atamaları için soyutlanmış eylem (Action) tanımı.
/// Bir eyleme birden fazla klavye, fare veya gamepad tuşu atanabilir (örn: "Jump" -> Space, GamePad.A).
/// </summary>
public class InputAction
{
    public string Name { get; }
    public List<Keys> Keys { get; } = new();
    public List<Buttons> Buttons { get; } = new();
    public List<MouseButton> MouseButtons { get; } = new();

    public InputAction(string name)
    {
        Name = name;
    }

    public InputAction AddKey(Keys key)
    {
        if (!Keys.Contains(key)) Keys.Add(key);
        return this;
    }

    public InputAction AddButton(Buttons button)
    {
        if (!Buttons.Contains(button)) Buttons.Add(button);
        return this;
    }

    public InputAction AddMouseButton(MouseButton mouseButton)
    {
        if (!MouseButtons.Contains(mouseButton)) MouseButtons.Add(mouseButton);
        return this;
    }
}
