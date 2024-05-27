namespace KazNet.Core
{
    public class NetworkMessagePermissionGroup
    {
        HashSet<NetworkMessagePermission> permissions = new();

        public NetworkMessagePermissionGroup() { }
        public NetworkMessagePermissionGroup(params NetworkMessagePermission[] _permission) { _permission.ToList().ForEach(x => Add(x)); }

        public bool Add(NetworkMessagePermission _permission) { return permissions.Add(_permission); }
        public bool Remove(NetworkMessagePermission _permission) { return permissions.Remove(_permission); }
        public bool CheckPermission(NetworkMessagePermission _permission) { return permissions.Contains(_permission); }
    }
}
