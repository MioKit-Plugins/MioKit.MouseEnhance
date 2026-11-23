namespace MouseEnhance.Services.Features;

internal sealed class FindMouseGestures
{
    private const int LeftCtrl = 0xA2;
    private const int RightCtrl = 0xA3;
    private const int CtrlHoldMs = 250;
    private const int CtrlReleaseGapMs = 500;
    private const int ShakeWindowMs = 650;
    private const int ShakeReversalPixels = 32;
    private const int MouseButtonCount = 5;

    private readonly object _sync = new();
    private readonly bool[] _heldKeys = new bool[256];
    private readonly Queue<long> _shakeReversals = new(12);

    private int _heldKeyCount;
    private int _heldMouseButtons;
    private int _pressedCtrl;
    private long _pressedCtrlAt;
    private int _releasedCtrl;
    private long _releasedCtrlAt;
    private bool _shakePrimed;
    private int _shakeStartX;
    private int _shakeStartY;
    private long _shakeLastProgressAt;
    private bool _shakeHorizontal;
    private int _shakeDirection;
    private int _shakeExtreme;

    public void KeyDown(int virtualKey, long nowMs, bool enabled)
    {
        if (virtualKey == 0 || (uint)virtualKey >= _heldKeys.Length)
            return;

        lock (_sync)
        {
            var repeated = _heldKeys[virtualKey];
            if (!repeated)
            {
                _heldKeys[virtualKey] = true;
                _heldKeyCount++;
            }

            if (!enabled || repeated || virtualKey is not (LeftCtrl or RightCtrl) ||
                _heldKeyCount != 1 || _heldMouseButtons != 0)
            {
                ResetCtrlSequence();
                return;
            }

            _pressedCtrl = virtualKey;
            _pressedCtrlAt = nowMs;
        }
    }

    public bool KeyUp(int virtualKey, long nowMs, bool enabled)
    {
        if (virtualKey == 0 || (uint)virtualKey >= _heldKeys.Length)
            return false;

        lock (_sync)
        {
            if (!_heldKeys[virtualKey])
                return false;

            _heldKeys[virtualKey] = false;
            _heldKeyCount--;

            if (!enabled)
            {
                ResetCtrlSequence();
                return false;
            }

            if (_pressedCtrl != virtualKey)
                return false;

            _pressedCtrl = 0;
            if (nowMs - _pressedCtrlAt is < 0 or > CtrlHoldMs)
            {
                ResetCtrlSequence();
                return false;
            }

            if (virtualKey == _releasedCtrl && nowMs - _releasedCtrlAt is >= 0 and <= CtrlReleaseGapMs)
            {
                ResetCtrlSequence();
                return true;
            }

            _releasedCtrl = virtualKey;
            _releasedCtrlAt = nowMs;
            return false;
        }
    }

    // Button indices: left, right, middle, X1, X2.
    public void SetMouseButton(int button, bool isDown)
    {
        if ((uint)button >= MouseButtonCount)
            return;

        lock (_sync)
        {
            var bit = 1 << button;
            var updated = isDown ? _heldMouseButtons | bit : _heldMouseButtons & ~bit;
            if (updated == _heldMouseButtons)
                return;

            _heldMouseButtons = updated;
            ResetShakeSequence();
            if (isDown)
                ResetCtrlSequence();
        }
    }

    public bool MouseMove(int x, int y, long nowMs, int reversalThreshold, bool enabled)
    {
        lock (_sync)
        {
            if (!enabled || _heldMouseButtons != 0)
            {
                if (_shakePrimed)
                    ResetShakeSequence();
                return false;
            }

            if (!_shakePrimed || nowMs - _shakeLastProgressAt is < 0 or > ShakeWindowMs)
            {
                ResetShakeSequence();
                _shakePrimed = true;
                _shakeStartX = x;
                _shakeStartY = y;
                _shakeLastProgressAt = nowMs;
                return false;
            }

            if (_shakeDirection == 0)
            {
                // Fix the axis once a deliberate motion establishes its direction.
                var dx = (long)x - _shakeStartX;
                var dy = (long)y - _shakeStartY;
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < ShakeReversalPixels)
                    return false;

                _shakeHorizontal = Math.Abs(dx) >= Math.Abs(dy);
                _shakeDirection = Math.Sign(_shakeHorizontal ? dx : dy);
                _shakeExtreme = _shakeHorizontal ? x : y;
                _shakeLastProgressAt = nowMs;
                return false;
            }

            var position = _shakeHorizontal ? x : y;
            var distanceInDirection = ((long)position - _shakeExtreme) * _shakeDirection;
            if (distanceInDirection > 0)
                _shakeExtreme = position;
            if (distanceInDirection > -ShakeReversalPixels)
                return false;

            _shakeDirection = -_shakeDirection;
            _shakeExtreme = position;
            _shakeLastProgressAt = nowMs;
            while (_shakeReversals.TryPeek(out var oldest) && nowMs - oldest > ShakeWindowMs)
                _shakeReversals.Dequeue();

            _shakeReversals.Enqueue(nowMs);
            if (_shakeReversals.Count < Math.Clamp(reversalThreshold, 3, 12))
                return false;

            ResetShakeSequence();
            return true;
        }
    }

    public void ResetSequences()
    {
        lock (_sync)
        {
            ResetCtrlSequence();
            ResetShakeSequence();
        }
    }

    public void ResetShake()
    {
        lock (_sync)
            ResetShakeSequence();
    }

    public void Reset()
    {
        lock (_sync)
        {
            Array.Clear(_heldKeys);
            _heldKeyCount = 0;
            _heldMouseButtons = 0;
            ResetCtrlSequence();
            ResetShakeSequence();
        }
    }

    private void ResetCtrlSequence()
    {
        _pressedCtrl = 0;
        _releasedCtrl = 0;
    }

    private void ResetShakeSequence()
    {
        _shakePrimed = false;
        _shakeDirection = 0;
        _shakeReversals.Clear();
    }
}
