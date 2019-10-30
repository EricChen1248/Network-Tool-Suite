namespace Network_Tool_Suite
{
    public static class Helper
    {
        public static unsafe void IntToByte(int num, byte[] bytes)
        {
            fixed (byte* pBytes = &bytes[0])
            {
                *(int*)pBytes = num;
            }
        }
        public static unsafe int ByteToInt(byte[] bytes)
        {
            fixed (byte* pBytes = &bytes[0])
            {
                return *(int*)pBytes;
            }
        }
    }
}
