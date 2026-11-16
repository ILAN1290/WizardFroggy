// Единая проверка "есть ли контроль над персонажем".
// НЕ путать с PlayerController — это отдельный класс-проверка.
public static class ControlGate
{
    // Ручной рубильник для систем, которые не переключают action map
    public static bool Blocked = false;

    public static bool HasControl
    {
        get
        {
            if (Blocked) return false;
            var input = InputManager.PlayerInput;
            return input != null
                && input.currentActionMap != null
                && input.currentActionMap.name == "Player";
        }
    }
}