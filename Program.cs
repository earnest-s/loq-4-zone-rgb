using System;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
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

        PlaySmoothWave(keyboard, profile, SwipeMode.Change, false);
    }

    static void PlaySwipe(Keyboard keyboard, Profile profile, SwipeMode mode, bool cleanWithBlack)
    {
        const byte STEPS = 150;
        
        byte[] changeRgbArray = profile.RgbArray();
        byte[] fillRgbArray = profile.RgbArray();
        byte[] usedColorsArray = new byte[12];

        byte steps = (byte)(STEPS / profile.Speed);

        for (int iteration = 0; iteration < 5; iteration++)
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
}
