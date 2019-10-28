namespace Network_Tool_Suite
{
    public static class Helper
    {
        public static void IntToByte(int num, byte[] bytes)
        {
            bytes[3] = (byte)(num >> 24);
            bytes[2] = (byte)(num >> 16);
            bytes[1] = (byte)(num >> 8);
            bytes[0] = (byte) num;
        }
    }
}
