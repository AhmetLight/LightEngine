using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace LightEngine.Core.Input;

/// <summary>
/// Giriş (Input) durumlarını okuyan, tuş kombinasyonlarını ve eksenleri işleyen somut servis sınıfı.
/// </summary>
public class InputManager : IInputService
{
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;

    private MouseState _currentMouse;
    private MouseState _previousMouse;

    private readonly GamePadState[] _currentGamePads = new GamePadState[4];
    private readonly GamePadState[] _previousGamePads = new GamePadState[4];

    private readonly Dictionary<string, InputAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InputAxis> _axes = new(StringComparer.OrdinalIgnoreCase);

    public Vector2 MousePosition => new(_currentMouse.X, _currentMouse.Y);
    public Vector2 MouseDelta => new(_currentMouse.X - _previousMouse.X, _currentMouse.Y - _previousMouse.Y);
    public int ScrollWheelDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    public InputManager()
    {
        _currentKeyboard = Keyboard.GetState();
        _previousKeyboard = _currentKeyboard;

        _currentMouse = Mouse.GetState();
        _previousMouse = _currentMouse;

        for (int i = 0; i < 4; i++)
        {
            _currentGamePads[i] = GamePad.GetState((PlayerIndex)i);
            _previousGamePads[i] = _currentGamePads[i];
        }
    }

    public void Update()
    {
        _previousKeyboard = _currentKeyboard;
        _currentKeyboard = Keyboard.GetState();

        _previousMouse = _currentMouse;
        _currentMouse = Mouse.GetState();

        for (int i = 0; i < 4; i++)
        {
            _previousGamePads[i] = _currentGamePads[i];
            _currentGamePads[i] = GamePad.GetState((PlayerIndex)i);
        }
    }

    #region Klavye

    public bool IsKeyDown(Keys key) => _currentKeyboard.IsKeyDown(key);
    public bool IsKeyUp(Keys key) => _currentKeyboard.IsKeyUp(key);
    public bool IsKeyPressed(Keys key) => _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    public bool IsKeyReleased(Keys key) => _currentKeyboard.IsKeyUp(key) && _previousKeyboard.IsKeyDown(key);

    #endregion

    #region Fare

    public bool IsMouseButtonDown(MouseButton button)
    {
        return GetMouseButtonState(_currentMouse, button) == ButtonState.Pressed;
    }

    public bool IsMouseButtonPressed(MouseButton button)
    {
        return GetMouseButtonState(_currentMouse, button) == ButtonState.Pressed &&
               GetMouseButtonState(_previousMouse, button) == ButtonState.Released;
    }

    public bool IsMouseButtonReleased(MouseButton button)
    {
        return GetMouseButtonState(_currentMouse, button) == ButtonState.Released &&
               GetMouseButtonState(_previousMouse, button) == ButtonState.Pressed;
    }

    private static ButtonState GetMouseButtonState(MouseState state, MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => state.LeftButton,
            MouseButton.Middle => state.MiddleButton,
            MouseButton.Right => state.RightButton,
            MouseButton.XButton1 => state.XButton1,
            MouseButton.XButton2 => state.XButton2,
            _ => ButtonState.Released
        };
    }

    #endregion

    #region Gamepad

    public bool IsButtonDown(Buttons button, PlayerIndex player = PlayerIndex.One)
    {
        return _currentGamePads[(int)player].IsButtonDown(button);
    }

    public bool IsButtonPressed(Buttons button, PlayerIndex player = PlayerIndex.One)
    {
        int idx = (int)player;
        return _currentGamePads[idx].IsButtonDown(button) && _previousGamePads[idx].IsButtonUp(button);
    }

    public bool IsButtonReleased(Buttons button, PlayerIndex player = PlayerIndex.One)
    {
        int idx = (int)player;
        return _currentGamePads[idx].IsButtonUp(button) && _previousGamePads[idx].IsButtonDown(button);
    }

    public Vector2 GetLeftThumbstick(PlayerIndex player = PlayerIndex.One)
    {
        return _currentGamePads[(int)player].ThumbSticks.Left;
    }

    public Vector2 GetRightThumbstick(PlayerIndex player = PlayerIndex.One)
    {
        return _currentGamePads[(int)player].ThumbSticks.Right;
    }

    public float GetLeftTrigger(PlayerIndex player = PlayerIndex.One)
    {
        return _currentGamePads[(int)player].Triggers.Left;
    }

    public float GetRightTrigger(PlayerIndex player = PlayerIndex.One)
    {
        return _currentGamePads[(int)player].Triggers.Right;
    }

    #endregion

    #region Action ve Axis Eşleme

    public void RegisterAction(InputAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _actions[action.Name] = action;
    }

    public void MapAction(string actionName, Keys key)
    {
        if (!_actions.TryGetValue(actionName, out var action))
        {
            action = new InputAction(actionName);
            _actions[actionName] = action;
        }
        action.AddKey(key);
    }

    public void MapAction(string actionName, Buttons button)
    {
        if (!_actions.TryGetValue(actionName, out var action))
        {
            action = new InputAction(actionName);
            _actions[actionName] = action;
        }
        action.AddButton(button);
    }

    public void MapAction(string actionName, MouseButton mouseButton)
    {
        if (!_actions.TryGetValue(actionName, out var action))
        {
            action = new InputAction(actionName);
            _actions[actionName] = action;
        }
        action.AddMouseButton(mouseButton);
    }

    public bool IsActionDown(string actionName)
    {
        if (!_actions.TryGetValue(actionName, out var action)) return false;

        for (int i = 0; i < action.Keys.Count; i++)
        {
            if (IsKeyDown(action.Keys[i])) return true;
        }

        for (int i = 0; i < action.Buttons.Count; i++)
        {
            if (IsButtonDown(action.Buttons[i])) return true;
        }

        for (int i = 0; i < action.MouseButtons.Count; i++)
        {
            if (IsMouseButtonDown(action.MouseButtons[i])) return true;
        }

        return false;
    }

    public bool IsActionPressed(string actionName)
    {
        if (!_actions.TryGetValue(actionName, out var action)) return false;

        for (int i = 0; i < action.Keys.Count; i++)
        {
            if (IsKeyPressed(action.Keys[i])) return true;
        }

        for (int i = 0; i < action.Buttons.Count; i++)
        {
            if (IsButtonPressed(action.Buttons[i])) return true;
        }

        for (int i = 0; i < action.MouseButtons.Count; i++)
        {
            if (IsMouseButtonPressed(action.MouseButtons[i])) return true;
        }

        return false;
    }

    public bool IsActionReleased(string actionName)
    {
        if (!_actions.TryGetValue(actionName, out var action)) return false;

        for (int i = 0; i < action.Keys.Count; i++)
        {
            if (IsKeyReleased(action.Keys[i])) return true;
        }

        for (int i = 0; i < action.Buttons.Count; i++)
        {
            if (IsButtonReleased(action.Buttons[i])) return true;
        }

        for (int i = 0; i < action.MouseButtons.Count; i++)
        {
            if (IsMouseButtonReleased(action.MouseButtons[i])) return true;
        }

        return false;
    }

    public void RegisterAxis(InputAxis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        _axes[axis.Name] = axis;
    }

    public void MapAxis(string axisName, Keys positive, Keys negative)
    {
        if (!_axes.TryGetValue(axisName, out var axis))
        {
            axis = new InputAxis(axisName);
            _axes[axisName] = axis;
        }
        axis.SetKeys(positive, negative);
    }

    public float GetAxis(string axisName)
    {
        if (!_axes.TryGetValue(axisName, out var axis)) return 0f;

        float value = 0f;

        if (axis.PositiveKey != Keys.None && IsKeyDown(axis.PositiveKey)) value += 1f;
        if (axis.NegativeKey != Keys.None && IsKeyDown(axis.NegativeKey)) value -= 1f;

        if (axis.PositiveButton != 0 && IsButtonDown(axis.PositiveButton)) value += 1f;
        if (axis.NegativeButton != 0 && IsButtonDown(axis.NegativeButton)) value -= 1f;

        if (axis.UseThumbstickX)
        {
            float stickX = GetLeftThumbstick().X;
            if (MathF.Abs(stickX) > 0.15f) value += stickX;
        }

        if (axis.UseThumbstickY)
        {
            float stickY = GetLeftThumbstick().Y;
            if (MathF.Abs(stickY) > 0.15f) value += stickY;
        }

        return Math.Clamp(value, -1.0f, 1.0f);
    }

    #endregion
}
