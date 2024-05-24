namespace KazNet.MsgSys
{
    public class NetworkMessageHandlerList
    {
        List<NetworkMessageHandler> messageHandlers = new();
        Dictionary<NetworkMessageHandler, int> messageHandlersDictionary = new();

        public void Add(NetworkMessageHandler _messageHandler)
        {
            if (messageHandlersDictionary.TryAdd(_messageHandler, messageHandlers.Count))
                messageHandlers.Add(_messageHandler);
        }
        public bool GetMessageHandlerId(NetworkMessageHandler _messageHandler, out int _id)
        {
            return messageHandlersDictionary.TryGetValue(_messageHandler, out _id);
        }
        public bool GetMessageHandler(int _id, out NetworkMessageHandler _messageHandler)
        {
            _messageHandler = messageHandlers.ElementAtOrDefault(_id);
            if (_messageHandler != null)
                return true;
            return false;
        }
    }
}
