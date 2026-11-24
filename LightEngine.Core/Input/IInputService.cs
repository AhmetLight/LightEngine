using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace LightEngine.Core.Input;

/// <summary>
/// Klavye, fare, gamepad ve tuş atama (Action/Axis mapping) işlemlerini yöneten servis arayüzü.
/// </summary>
public interface IInputService
{
    // Klavye
    bool IsKeyDown(Keys key);
    bool IsKeyUp(Keys key);
    bool IsKeyPressed(Keys key);
    bool IsKeyReleased(Keys key);

    // Fare
    Vector2 MousePosition { get; }
    Vector2 MouseDelta { get; }
    int ScrollWheelDelta { get; }
    bool IsMouseButtonDown(MouseButton button);
    bool IsMouseButtonPressed(MouseButton button);
    bool IsMouseButtonReleased(MouseButton button);

    // Gamepad
    bool IsButtonDown(Buttons button, PlayerIndex player = PlayerIndex.One);
    bool IsButtonPressed(Buttons button, PlayerIndex player = PlayerIndex.One);
    bool IsButtonReleased(Buttons button, PlayerIndex player = PlayerIndex.One);
    Vector2 GetLeftThumbstick(PlayerIndex player = PlayerIndex.One);
    Vector2 GetRightThumbstick(PlayerIndex player = PlayerIndex.One);
    float GetLeftTrigger(PlayerIndex player = PlayerIndex.One);
    float GetRightTrigger(PlayerIndex player = PlayerIndex.One);

    // Aksiyon ve Eksen Eşleme (Action / Axis Mapping)
    void RegisterAction(InputAction action);
    void MapAction(string actionName, Keys key);
    void MapAction(string actionName, Buttons button);
    void MapAction(string actionName, MouseButton mouseButton);
    bool IsActionDown(string actionName);
    bool IsActionPressed(string actionName);
    bool IsActionReleased(string actionName);

    void RegisterAxis(InputAxis axis);
    void MapAxis(string axisName, Keys positive, Keys negative);
    float GetAxis(string axisName);

    /// <summary>
    /// Her kare başında giriş durumlarını yeniler.
    /// </summary>
    void Update();
}
