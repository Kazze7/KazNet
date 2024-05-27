namespace KazNet.Core
{
    public class ClientEntity
    {
        private ulong mUID;
        public ulong UID { get { return mUID; } }
        public NetworkMessagePermissionGroup permissionGroup;

        public ClientEntity(ulong _mUID, NetworkMessagePermissionGroup _permissionGroup)
        {
            mUID = _mUID;
            permissionGroup = _permissionGroup;
        }
    }
}
