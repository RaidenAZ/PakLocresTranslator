namespace UEPakTool.Core;

public static class AlignHelper
{
    public static long Align(long value, int alignment) =>
        (value + alignment - 1) & ~(alignment - 1);

    public static int Align(int value, int alignment) =>
        (int)((value + (long)alignment - 1) & ~(alignment - 1));
}
