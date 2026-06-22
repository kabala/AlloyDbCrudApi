namespace AlloyDbCrudApi.Domain.Enums;

public enum StoreChannel
{
    Physical = 0,
    Online = 1,
}

public static class StoreChannelNames
{
    public const string Physical = "Physical";
    public const string Online = "Online";

    public static string Name(StoreChannel c) => c == StoreChannel.Online ? Online : Physical;
}
