namespace KazNet.MsgSys
{
    public static class NetworkMessageHandlerList
    {
        static List<NetworkMessageHandler> messageHandlers = new();
        static Dictionary<NetworkMessageHandler, int> messageHandlersDictionary = new();

        public static void Add(NetworkMessageHandler _messageHandler)
        {
            if (messageHandlersDictionary.TryAdd(_messageHandler, messageHandlers.Count))
                messageHandlers.Add(_messageHandler);
        }
        public static int GetId(NetworkMessageHandler _messageHandler)
        {
            messageHandlersDictionary.TryGetValue(_messageHandler, out int id);
            return id;
        }
        public static NetworkMessageHandler GetMessageHandler(int _id)
        {
            return messageHandlers[_id];
        }
    }
}
