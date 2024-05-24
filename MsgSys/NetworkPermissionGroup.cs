namespace KazNet.MsgSys
{
    public class NetworkPermissionGroup
    {
        HashSet<NetworkPermission> permissions = new();

        public NetworkPermissionGroup() { }
        public NetworkPermissionGroup(params NetworkPermission[] _permission) { _permission.ToList().ForEach(x => Add(x)); }

        public bool Add(NetworkPermission _permission) { return permissions.Add(_permission); }
        public bool Remove(NetworkPermission _permission) { return permissions.Remove(_permission); }
        public bool CheckPermission(NetworkPermission _permission) { return permissions.Contains(_permission); }
    }
}
