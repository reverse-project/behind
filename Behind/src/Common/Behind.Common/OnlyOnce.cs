namespace Behind.Common;

public sealed class OnlyOnce
{
    private bool _once = true;

    public bool Apply()
    {
        bool oldValue = _once;
        _once = false;
        return oldValue;
    }
}