using System;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using HidSharp;

enum BaseEffect
{
    Static,
    Breath,
    Smooth,
    LeftWave,
    RightWave
}

enum Direction
{
    Left,
    Right
}

enum SwipeMode
{
    Change,
    Fill
}

enum RippleMove
{
    Center,
    Left,
    Right,
    Off
}

class LightingState
{
    public BaseEffect EffectType = BaseEffect.Static;
    public byte Speed = 1;
    public byte Brightness = 1;
    public byte[] RgbValues = new byte[12];
}

class Keyboard
{
    private HidDevice _device;
    private LightingState _currentState;
    private bool _stopSignal;

    public Keyboard(HidDevice device)
    {
        _device = device;
        _currentState = new LightingState();
        _stopSignal = false;
        Refresh();
    }

    private byte[] BuildPayload()
    {
        byte[] payload = new byte[33];
        payload[0] = 0xcc;
        payload[1] = 0x16;
        
        switch (_currentState.EffectType)
        {
            case BaseEffect.Static:
                payload[2] = 0x01;
                break;
            case BaseEffect.Breath:
                payload[2] = 0x03;
                break;
            case BaseEffect.Smooth:
                payload[2] = 0x06;
                break;
            case BaseEffect.LeftWave:
                payload[2] = 0x04;
                payload[19] = 0x1;
                break;
            case BaseEffect.RightWave:
                payload[2] = 0x04;
                payload[18] = 0x1;
                break;
        }

        payload[3] = _currentState.Speed;
        payload[4] = _currentState.Brightness;

        if (_currentState.EffectType == BaseEffect.Static || _currentState.EffectType == BaseEffect.Breath)
        {
            Array.Copy(_currentState.RgbValues, 0, payload, 5, 12);
        }

        return payload;
    }

    public void Refresh()
    {
        byte[] payload = BuildPayload();
        try
        {
            var stream = _device.Open();
            stream.SetFeature(payload);
            stream.Close();
        }
        catch { }
    }

    public void SetEffect(BaseEffect effect)
    {
        _currentState.EffectType = effect;
        Refresh();
    }

    public void SetSpeed(byte speed)
    {
        _currentState.Speed = Math.Clamp(speed, (byte)1, (byte)4);
        Refresh();
    }

    public void SetBrightness(byte brightness)
    {
        _currentState.Brightness = Math.Clamp(brightness, (byte)1, (byte)2);
        Refresh();
    }

    public void SetZoneByIndex(byte zoneIndex, byte[] newValues)
    {
        for (int i = 0; i < newValues.Length; i++)
        {
            int fullIndex = zoneIndex * 3 + i;
            _currentState.RgbValues[fullIndex] = newValues[i];
        }
        Refresh();
    }

    public void SetColorsTo(byte[] newValues)
    {
        if (_currentState.EffectType == BaseEffect.Static || _currentState.EffectType == BaseEffect.Breath)
        {
            Array.Copy(newValues, _currentState.RgbValues, 12);
            Refresh();
        }
    }

    public void SolidSetColorsTo(byte[] newValues)
    {
        if (_currentState.EffectType == BaseEffect.Static || _currentState.EffectType == BaseEffect.Breath)
        {
            for (int i = 0; i < 12; i += 3)
            {
                _currentState.RgbValues[i] = newValues[0];
                _currentState.RgbValues[i + 1] = newValues[1];
                _currentState.RgbValues[i + 2] = newValues[2];
            }
            Refresh();
        }
    }

    public void TransitionColorsTo(byte[] targetColors, byte steps, ulong delayBetweenSteps)
    {
        if (_currentState.EffectType == BaseEffect.Static || _currentState.EffectType == BaseEffect.Breath)
        {
            float[] newValues = new float[12];
            float[] colorDifferences = new float[12];
            
            for (int i = 0; i < 12; i++)
            {
                newValues[i] = _currentState.RgbValues[i];
                colorDifferences[i] = (targetColors[i] - _currentState.RgbValues[i]) / (float)steps;
            }

            if (!_stopSignal)
            {
                for (byte stepNum = 1; stepNum <= steps; stepNum++)
                {
                    if (_stopSignal)
                        break;

                    for (int index = 0; index < 12; index++)
                    {
                        newValues[index] += colorDifferences[index];
                    }

                    for (int i = 0; i < 12; i++)
                    {
                        _currentState.RgbValues[i] = (byte)newValues[i];
                    }

                    Refresh();
                    Thread.Sleep((int)delayBetweenSteps);
                }
                SetColorsTo(targetColors);
            }
        }
    }

    public void Stop()
    {
        _stopSignal = true;
    }
}

class Profile
{
    public byte[][] RgbZones = new byte[4][];
    public byte Speed = 1;
    public Direction Direction = Direction.Left;

    public Profile()
    {
        for (int i = 0; i < 4; i++)
        {
            RgbZones[i] = new byte[3];
        }
    }

    public byte[] RgbArray()
    {
        byte[] result = new byte[12];
        for (int i = 0; i < 4; i++)
        {
            Array.Copy(RgbZones[i], 0, result, i * 3, 3);
        }
        return result;
    }
}

class Program
{
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    static void Main()
    {
        var device = DeviceList.Local.GetHidDevices(0x048d, 0xc993)
            .FirstOrDefault(d => d.GetMaxFeatureReportLength() == 33);

        if (device == null)
        {
            Console.WriteLine("Device not found");
            return;
        }

        var keyboard = new Keyboard(device);
        keyboard.SetEffect(BaseEffect.Static);
        keyboard.SetBrightness(2);
        keyboard.SetSpeed(1);

        var profile = new Profile();
        profile.RgbZones[0] = new byte[] { 255, 0, 0 };
        profile.RgbZones[1] = new byte[] { 0, 255, 0 };
        profile.RgbZones[2] = new byte[] { 0, 0, 255 };
        profile.RgbZones[3] = new byte[] { 255, 255, 0 };
        profile.Speed = 2;
        profile.Direction = Direction.Left;

        profile.RgbZones[0] = new byte[] { 255, 0, 0 };
        profile.RgbZones[1] = new byte[] { 0, 255, 0 };
        profile.RgbZones[2] = new byte[] { 0, 0, 255 };
        profile.RgbZones[3] = new byte[] { 255, 0, 255 };

        PlayAmbient(keyboard, 5, 1.5f);
    }

    static void PlaySwipe(Keyboard keyboard, Profile profile, SwipeMode mode, bool cleanWithBlack)
    {
        const byte STEPS = 150;
        
        byte[] changeRgbArray = profile.RgbArray();
        byte[] fillRgbArray = profile.RgbArray();
        byte[] usedColorsArray = new byte[12];

        byte steps = (byte)(STEPS / profile.Speed);

        while (true)
        {
            switch (mode)
            {
                case SwipeMode.Change:
                    switch (profile.Direction)
                    {
                        case Direction.Left:
                            RotateRight(changeRgbArray, 3);
                            break;
                        case Direction.Right:
                            RotateLeft(changeRgbArray, 3);
                            break;
                    }
                    keyboard.TransitionColorsTo(changeRgbArray, steps, 10);
                    break;

                case SwipeMode.Fill:
                    int[] range;
                    if (profile.Direction == Direction.Left)
                        range = new int[] { 0, 1, 2, 3 };
                    else
                        range = new int[] { 3, 2, 1, 0 };

                    for (int i = 0; i < range.Length; i++)
                    {
                        for (int j = 0; j < range.Length; j++)
                        {
                            usedColorsArray[range[j] * 3] = fillRgbArray[range[i] * 3];
                            usedColorsArray[range[j] * 3 + 1] = fillRgbArray[range[i] * 3 + 1];
                            usedColorsArray[range[j] * 3 + 2] = fillRgbArray[range[i] * 3 + 2];
                            keyboard.TransitionColorsTo(usedColorsArray, steps, 1);
                        }
                        if (cleanWithBlack)
                        {
                            for (int j = 0; j < range.Length; j++)
                            {
                                usedColorsArray[range[j] * 3] = 0;
                                usedColorsArray[range[j] * 3 + 1] = 0;
                                usedColorsArray[range[j] * 3 + 2] = 0;
                                keyboard.TransitionColorsTo(usedColorsArray, steps, 1);
                            }
                        }
                    }
                    break;
            }

            Thread.Sleep(20);
        }
    }

    static void RotateLeft(byte[] array, int positions)
    {
        byte[] temp = new byte[positions];
        Array.Copy(array, 0, temp, 0, positions);
        Array.Copy(array, positions, array, 0, array.Length - positions);
        Array.Copy(temp, 0, array, array.Length - positions, positions);
    }

    static void RotateRight(byte[] array, int positions)
    {
        byte[] temp = new byte[positions];
        Array.Copy(array, array.Length - positions, temp, 0, positions);
        Array.Copy(array, 0, array, positions, array.Length - positions);
        Array.Copy(temp, 0, array, 0, positions);
    }

    static bool IsAnyKeyPressed()
    {
        for (int i = 8; i < 256; i++)
        {
            if ((GetAsyncKeyState(i) & 0x8000) != 0)
                return true;
        }
        return false;
    }

    static void PlayFade(Keyboard keyboard, Profile profile)
    {
        DateTime now = DateTime.Now;
        bool hasFaded = false;

        while (true)
        {
            if (IsAnyKeyPressed())
            {
                keyboard.SetColorsTo(profile.RgbArray());
                now = DateTime.Now;
                hasFaded = false;
            }
            else
            {
                if (!hasFaded && (DateTime.Now - now).TotalSeconds > 20 / (double)profile.Speed)
                {
                    keyboard.TransitionColorsTo(new byte[12], 230, 3);
                    hasFaded = true;
                }
                else
                {
                    Thread.Sleep(20);
                }
            }

            Thread.Sleep(5);
        }
    }

    static void PlaySmoothWave(Keyboard keyboard, Profile profile, SwipeMode mode, bool cleanWithBlack)
    {
        const byte STEPS = 150;
        
        byte[] changeRgbArray = profile.RgbArray();
        byte[] fillRgbArray = profile.RgbArray();
        byte[] usedColorsArray = new byte[12];

        byte steps = (byte)(STEPS / profile.Speed);

        while (true)
        {
            switch (mode)
            {
                case SwipeMode.Change:
                    switch (profile.Direction)
                    {
                        case Direction.Left:
                            RotateRight(changeRgbArray, 3);
                            break;
                        case Direction.Right:
                            RotateLeft(changeRgbArray, 3);
                            break;
                    }
                    keyboard.TransitionColorsTo(changeRgbArray, steps, 10);
                    break;

                case SwipeMode.Fill:
                    int[] range;
                    if (profile.Direction == Direction.Left)
                        range = new int[] { 0, 1, 2, 3 };
                    else
                        range = new int[] { 3, 2, 1, 0 };

                    for (int i = 0; i < range.Length; i++)
                    {
                        for (int j = 0; j < range.Length; j++)
                        {
                            usedColorsArray[range[j] * 3] = fillRgbArray[range[i] * 3];
                            usedColorsArray[range[j] * 3 + 1] = fillRgbArray[range[i] * 3 + 1];
                            usedColorsArray[range[j] * 3 + 2] = fillRgbArray[range[i] * 3 + 2];
                            keyboard.TransitionColorsTo(usedColorsArray, steps, 1);
                        }
                        if (cleanWithBlack)
                        {
                            for (int j = 0; j < range.Length; j++)
                            {
                                usedColorsArray[range[j] * 3] = 0;
                                usedColorsArray[range[j] * 3 + 1] = 0;
                                usedColorsArray[range[j] * 3 + 2] = 0;
                                keyboard.TransitionColorsTo(usedColorsArray, steps, 1);
                            }
                        }
                    }
                    break;
            }

            Thread.Sleep(20);
        }
    }

    static void PlayRipple(Keyboard keyboard, Profile profile)
    {
        HashSet<int>[] zonePressed = new HashSet<int>[4]
        {
            new HashSet<int>(),
            new HashSet<int>(),
            new HashSet<int>(),
            new HashSet<int>()
        };
        RippleMove[] zoneState = new RippleMove[4] { RippleMove.Off, RippleMove.Off, RippleMove.Off, RippleMove.Off };

        DateTime lastStepTime = DateTime.Now;

        while (true)
        {
            for (int vk = 0; vk < 256; vk++)
            {
                short keyState = GetAsyncKeyState(vk);
                bool isPressed = (keyState & 0x8000) != 0;

                if (isPressed)
                {
                    int zone = GetKeyZone(vk);
                    if (zone != -1)
                    {
                        zonePressed[zone].Add(vk);
                    }
                }
                else
                {
                    for (int i = 0; i < 4; i++)
                    {
                        zonePressed[i].Remove(vk);
                    }
                }
            }

            zoneState = AdvanceZoneState(zoneState, ref lastStepTime, profile.Speed);

            for (int i = 0; i < zonePressed.Length; i++)
            {
                if (zonePressed[i].Count > 0)
                {
                    zoneState[i] = RippleMove.Center;
                }
            }

            byte[] rgbArray = profile.RgbArray();
            byte[] finalArr = new byte[12];

            for (int i = 0; i < zoneState.Length; i++)
            {
                if (zoneState[i] != RippleMove.Off)
                {
                    Array.Copy(rgbArray, i * 3, finalArr, i * 3, 3);
                }
            }

            keyboard.TransitionColorsTo(finalArr, 20, 0);
            Thread.Sleep(50);
        }
    }

    static RippleMove[] AdvanceZoneState(RippleMove[] zoneState, ref DateTime lastStepTime, byte speed)
    {
        DateTime now = DateTime.Now;

        if ((now - lastStepTime).TotalMilliseconds > (200.0 / speed))
        {
            RippleMove[] newState = new RippleMove[4] { RippleMove.Off, RippleMove.Off, RippleMove.Off, RippleMove.Off };

            lastStepTime = now;

            for (int i = 0; i < zoneState.Length; i++)
            {
                switch (zoneState[i])
                {
                    case RippleMove.Left:
                        if (i != 0)
                        {
                            newState[i - 1] = RippleMove.Left;
                        }
                        break;

                    case RippleMove.Right:
                        if (i < 3)
                        {
                            newState[i + 1] = RippleMove.Right;
                        }
                        break;
                }
            }

            for (int i = 0; i < zoneState.Length; i++)
            {
                if (zoneState[i] == RippleMove.Center)
                {
                    if (i != 0)
                    {
                        newState[i - 1] = RippleMove.Left;
                    }

                    if (i < 3)
                    {
                        newState[i + 1] = RippleMove.Right;
                    }
                }
            }

            return newState;
        }
        else
        {
            return zoneState;
        }
    }

    static int GetKeyZone(int vk)
    {
        if (vk >= 0x70 && vk <= 0x73)
            return 0;
        if (vk == 0xC0)
            return 0;
        if (vk >= 0x31 && vk <= 0x34)
            return 0;
        if (vk == 0x09 || vk == 0x51 || vk == 0x57 || vk == 0x45)
            return 0;
        if (vk == 0x14 || vk == 0x41 || vk == 0x53 || vk == 0x44)
            return 0;
        if (vk == 0xA0 || vk == 0x5A || vk == 0x58)
            return 0;
        if (vk == 0xA2 || vk == 0x5B || vk == 0xA4)
            return 0;

        if (vk >= 0x74 && vk <= 0x79)
            return 1;
        if (vk >= 0x35 && vk <= 0x39)
            return 1;
        if (vk >= 0x52 && vk <= 0x55)
            return 1;
        if (vk == 0x49)
            return 1;
        if (vk >= 0x46 && vk <= 0x4B)
            return 1;
        if (vk >= 0x43 && vk <= 0x4E)
            return 1;
        if (vk == 0xBC || vk == 0x20 || vk == 0xA5)
            return 1;

        if (vk == 0x7A || vk == 0x7B || vk == 0x2D || vk == 0x2E)
            return 2;
        if (vk == 0x30 || vk == 0xBD || vk == 0xBB || vk == 0x08)
            return 2;
        if (vk == 0x4F || vk == 0x50 || vk == 0xDB || vk == 0xDD || vk == 0x0D)
            return 2;
        if (vk == 0x4C || vk == 0xBA || vk == 0xDE || vk == 0xDC)
            return 2;
        if (vk == 0xBE || vk == 0xBF || vk == 0xA1)
            return 2;
        if (vk == 0xA3 || vk == 0x26 || vk == 0x28 || vk == 0x25 || vk == 0x27)
            return 2;

        if (vk == 0x24 || vk == 0x23 || vk == 0x21 || vk == 0x22)
            return 3;
        if (vk >= 0x6F && vk <= 0x6D)
            return 3;
        if (vk >= 0x67 && vk <= 0x69)
            return 3;
        if (vk >= 0x64 && vk <= 0x6B)
            return 3;
        if (vk >= 0x61 && vk <= 0x63)
            return 3;
        if (vk == 0x60)
            return 3;

        return -1;
    }

    static void PlayChristmas(Keyboard keyboard)
    {
        byte[][] xmasColorArray = new byte[4][]
        {
            new byte[] { 255, 10, 10 },
            new byte[] { 255, 255, 20 },
            new byte[] { 30, 255, 30 },
            new byte[] { 70, 70, 255 }
        };
        int subeffectCount = 4;
        int lastSubeffect = -1;
        Random rng = new Random();

        while (true)
        {
            int subeffect = rng.Next(0, subeffectCount);
            while (lastSubeffect == subeffect)
            {
                subeffect = rng.Next(0, subeffectCount);
            }
            lastSubeffect = subeffect;

            switch (subeffect)
            {
                case 0:
                    for (int i = 0; i < 3; i++)
                    {
                        foreach (byte[] colors in xmasColorArray)
                        {
                            keyboard.SolidSetColorsTo(colors);
                            Thread.Sleep(500);
                        }
                    }
                    break;

                case 1:
                    {
                        int color1Index = rng.Next(0, 4);
                        byte[] usedColors1 = xmasColorArray[color1Index];

                        int color2Index = rng.Next(0, 4);
                        while (color1Index == color2Index)
                        {
                            color2Index = rng.Next(0, 4);
                        }
                        byte[] usedColors2 = xmasColorArray[color2Index];

                        for (int i = 0; i < 4; i++)
                        {
                            keyboard.SolidSetColorsTo(usedColors1);
                            Thread.Sleep(400);
                            keyboard.SolidSetColorsTo(usedColors2);
                            Thread.Sleep(400);
                        }
                    }
                    break;

                case 2:
                    {
                        byte steps = 100;
                        keyboard.TransitionColorsTo(new byte[12], steps, 1);
                        byte[] usedColorsArray = new byte[12];
                        int leftOrRight = rng.Next(0, 2);

                        int[] range;
                        if (leftOrRight == 0)
                        {
                            range = new int[] { 0, 1, 2, 3 };
                        }
                        else
                        {
                            range = new int[] { 3, 2, 1, 0 };
                        }

                        foreach (byte[] color in xmasColorArray)
                        {
                            foreach (int j in range)
                            {
                                usedColorsArray[j * 3] = color[0];
                                usedColorsArray[j * 3 + 1] = color[1];
                                usedColorsArray[j * 3 + 2] = color[2];
                                keyboard.TransitionColorsTo(usedColorsArray, steps, 1);
                            }
                            foreach (int j in range)
                            {
                                usedColorsArray[j * 3] = 0;
                                usedColorsArray[j * 3 + 1] = 0;
                                usedColorsArray[j * 3 + 2] = 0;
                                keyboard.TransitionColorsTo(usedColorsArray, steps, 1);
                            }
                        }
                    }
                    break;

                case 3:
                    {
                        byte[] state1 = new byte[] { 255, 255, 255, 0, 0, 0, 255, 255, 255, 0, 0, 0 };
                        byte[] state2 = new byte[] { 0, 0, 0, 255, 255, 255, 0, 0, 0, 255, 255, 255 };
                        byte steps = 30;
                        for (int i = 0; i < 4; i++)
                        {
                            keyboard.TransitionColorsTo(state1, steps, 1);
                            Thread.Sleep(400);
                            keyboard.TransitionColorsTo(state2, steps, 1);
                            Thread.Sleep(400);
                        }
                    }
                    break;
            }
        }
    }

    static void PlayAmbient(Keyboard keyboard, byte fps, float saturationBoost)
    {
        int screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        int screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
        long nanosPerFrame = 1_000_000_000 / fps;
        TimeSpan secondsPerFrame = TimeSpan.FromMilliseconds(nanosPerFrame / 1_000_000.0);

        while (true)
        {
            DateTime now = DateTime.Now;

            try
            {
                using (Bitmap screenshot = new Bitmap(screenWidth, screenHeight, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(screenshot))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, screenshot.Size);
                    }

                    using (Bitmap resized = new Bitmap(4, 1))
                    {
                        using (Graphics g = Graphics.FromImage(resized))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(screenshot, 0, 0, 4, 1);
                        }

                        byte[] rgb = new byte[12];
                        for (int i = 0; i < 4; i++)
                        {
                            Color pixel = resized.GetPixel(i, 0);
                            float r = pixel.R / 255.0f;
                            float g_val = pixel.G / 255.0f;
                            float b = pixel.B / 255.0f;

                            float max = Math.Max(r, Math.Max(g_val, b));
                            float min = Math.Min(r, Math.Min(g_val, b));
                            float delta = max - min;

                            float h = 0;
                            float s = 0;
                            float v = max;

                            if (delta != 0)
                            {
                                s = delta / max;

                                if (r == max)
                                    h = (g_val - b) / delta + (g_val < b ? 6 : 0);
                                else if (g_val == max)
                                    h = (b - r) / delta + 2;
                                else
                                    h = (r - g_val) / delta + 4;

                                h /= 6;
                            }

                            s = Math.Min(1.0f, s * saturationBoost);

                            float c = v * s;
                            float x = c * (1 - Math.Abs((h * 6) % 2 - 1));
                            float m = v - c;

                            float r_out = 0, g_out = 0, b_out = 0;

                            if (h < 1.0f / 6.0f)
                            {
                                r_out = c; g_out = x; b_out = 0;
                            }
                            else if (h < 2.0f / 6.0f)
                            {
                                r_out = x; g_out = c; b_out = 0;
                            }
                            else if (h < 3.0f / 6.0f)
                            {
                                r_out = 0; g_out = c; b_out = x;
                            }
                            else if (h < 4.0f / 6.0f)
                            {
                                r_out = 0; g_out = x; b_out = c;
                            }
                            else if (h < 5.0f / 6.0f)
                            {
                                r_out = x; g_out = 0; b_out = c;
                            }
                            else
                            {
                                r_out = c; g_out = 0; b_out = x;
                            }

                            rgb[i * 3] = (byte)((r_out + m) * 255);
                            rgb[i * 3 + 1] = (byte)((g_out + m) * 255);
                            rgb[i * 3 + 2] = (byte)((b_out + m) * 255);
                        }

                        keyboard.SetColorsTo(rgb);
                    }
                }
            }
            catch
            {
            }

            TimeSpan elapsedTime = DateTime.Now - now;
            if (elapsedTime < secondsPerFrame)
            {
                Thread.Sleep(secondsPerFrame - elapsedTime);
            }
        }
    }
}
