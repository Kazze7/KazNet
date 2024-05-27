namespace KazNet.Core
{
    public class NetworkMessageHandlerList
    {
        List<NetworkMessageHandler> messageHandlers = new();
        Dictionary<NetworkMessageHandler, ushort> messageHandlersIndex = new();

        public NetworkMessageHandlerList() { }
        public NetworkMessageHandlerList(params NetworkMessageHandler[] _messageHandler) { _messageHandler.ToList().ForEach(x => Add(x)); }

        public void Add(NetworkMessageHandler _messageHandler)
        {
            if (messageHandlersIndex.TryAdd(_messageHandler, (ushort)messageHandlers.Count))
                messageHandlers.Add(_messageHandler);
        }
        public bool GetMessageHandlerId(NetworkMessageHandler _messageHandler, out ushort _id)
        {
            return messageHandlersIndex.TryGetValue(_messageHandler, out _id);
        }
        public bool GetMessageHandler(ushort _handlerId, out NetworkMessageHandler _messageHandler)
        {
            _messageHandler = messageHandlers.ElementAtOrDefault(_handlerId);
            if (_messageHandler != null)
                return true;
            return false;
        }
    }
}
