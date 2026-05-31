namespace HiAuRo;

public static partial class Data
{
    /// <summary>聊天输出数据入口，供 ACR 发送宏和提示。</summary>
    public static class Chat
    {
        /// <summary>发送一行聊天消息。</summary>
        public static void SendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (message.StartsWith('/'))
                DService.Instance().Command.ProcessCommand(message);
            else
                DService.Instance().Chat.Print(message);
        }
    }
}
