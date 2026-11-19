using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ReinoRunas
{
    /// <summary>
    /// Entrada unificada de TECLADO y CONTROL (gamepad). Toda la navegación del juego pasa por acá.
    /// - Navegar: flechas / WASD  ·  cruceta / stick izquierdo
    /// - Confirmar: Enter / Espacio / Z  ·  botón Sur (A / Cruz)
    /// - Volver: Esc / Retroceso / X  ·  botón Este (B / Círculo)
    /// - Acelerar combate (mantener): Shift / Tab  ·  gatillo derecho (RT / R2)
    /// Repite la navegación al mantener la dirección y recuerda el último dispositivo usado
    /// para mostrar los íconos correctos en la interfaz.
    /// </summary>
    public static class RInput
    {
        public enum Device { Keyboard, Gamepad }
        public static Device LastDevice { get; private set; } = Device.Keyboard;

        static int cachedFrame = -1;
        static Vector2Int nav, lastDir;
        static float repeatTimer;
        static bool confirm, cancel;

        public static Vector2Int Navigate { get { Refresh(); return nav; } }
        public static bool Confirm { get { Refresh(); return confirm; } }
        public static bool Cancel { get { Refresh(); return cancel; } }

        public static string ConfirmLabel => LastDevice == Device.Gamepad ? "(A)" : "[Enter]";
        public static string CancelLabel => LastDevice == Device.Gamepad ? "(B)" : "[Esc]";
        public static string NavLabel => LastDevice == Device.Gamepad ? "(Cruceta/Stick)" : "[Flechas]";
        public static string FastLabel => LastDevice == Device.Gamepad ? "(RT)" : "[Shift]";

        static void Refresh()
        {
            if (cachedFrame == Time.frameCount) return;
            cachedFrame = Time.frameCount;

            ReadButtons(out confirm, out cancel);
            Vector2Int dir = ReadDirection();

            if (dir == Vector2Int.zero)
            {
                lastDir = Vector2Int.zero;
                nav = Vector2Int.zero;
                return;
            }
            if (dir != lastDir)
            {
                nav = dir;
                lastDir = dir;
                repeatTimer = 0.35f;
            }
            else
            {
                repeatTimer -= Time.unscaledDeltaTime;
                if (repeatTimer <= 0f)
                {
                    nav = dir;
                    repeatTimer = 0.09f;
                }
                else nav = Vector2Int.zero;
            }
        }

        static Vector2Int ToDir(float x, float y)
        {
            if (Mathf.Abs(x) < 0.5f && Mathf.Abs(y) < 0.5f) return Vector2Int.zero;
            if (Mathf.Abs(y) >= Mathf.Abs(x)) return new Vector2Int(0, y > 0 ? 1 : -1);
            return new Vector2Int(x > 0 ? 1 : -1, 0);
        }

#if ENABLE_INPUT_SYSTEM
        static Vector2Int ReadDirection()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            float x = 0f, y = 0f;
            if (k != null)
            {
                if (k.upArrowKey.isPressed || k.wKey.isPressed) y += 1f;
                if (k.downArrowKey.isPressed || k.sKey.isPressed) y -= 1f;
                if (k.rightArrowKey.isPressed || k.dKey.isPressed) x += 1f;
                if (k.leftArrowKey.isPressed || k.aKey.isPressed) x -= 1f;
                if (x != 0f || y != 0f) LastDevice = Device.Keyboard;
            }
            if (g != null && x == 0f && y == 0f)
            {
                Vector2 v = g.dpad.ReadValue() + g.leftStick.ReadValue();
                x = v.x;
                y = v.y;
                if (Mathf.Abs(x) > 0.5f || Mathf.Abs(y) > 0.5f) LastDevice = Device.Gamepad;
            }
            return ToDir(x, y);
        }

        static void ReadButtons(out bool ok, out bool back)
        {
            ok = back = false;
            var k = Keyboard.current;
            var g = Gamepad.current;
            if (k != null)
            {
                bool kOk = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame;
                bool kBack = k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame || k.xKey.wasPressedThisFrame;
                if (kOk || kBack) LastDevice = Device.Keyboard;
                ok |= kOk;
                back |= kBack;
            }
            if (g != null)
            {
                bool gOk = g.buttonSouth.wasPressedThisFrame;
                bool gBack = g.buttonEast.wasPressedThisFrame;
                if (gOk || gBack) LastDevice = Device.Gamepad;
                ok |= gOk;
                back |= gBack;
            }
        }

        public static bool FastForward
        {
            get
            {
                var k = Keyboard.current;
                var g = Gamepad.current;
                return (k != null && (k.leftShiftKey.isPressed || k.tabKey.isPressed)) ||
                       (g != null && g.rightTrigger.ReadValue() > 0.5f);
            }
        }
#else
        static Vector2Int ReadDirection()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) y -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) x -= 1f;
            if (x != 0f || y != 0f) { LastDevice = Device.Keyboard; return ToDir(x, y); }
            x = Input.GetAxisRaw("Horizontal");
            y = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(x) > 0.5f || Mathf.Abs(y) > 0.5f) LastDevice = Device.Gamepad;
            return ToDir(x, y);
        }

        static void ReadButtons(out bool ok, out bool back)
        {
            bool kOk = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z);
            bool kBack = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.X);
            bool gOk = Input.GetKeyDown(KeyCode.JoystickButton0);
            bool gBack = Input.GetKeyDown(KeyCode.JoystickButton1);
            if (kOk || kBack) LastDevice = Device.Keyboard;
            if (gOk || gBack) LastDevice = Device.Gamepad;
            ok = kOk || gOk;
            back = kBack || gBack;
        }

        public static bool FastForward => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.Tab) || Input.GetKey(KeyCode.JoystickButton5);
#endif
    }
}
