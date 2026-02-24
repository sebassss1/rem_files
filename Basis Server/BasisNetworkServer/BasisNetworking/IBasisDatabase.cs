using System.Collections.Generic;

namespace BasisNetworkServer.BasisNetworking
{
    public interface IBasisDatabase
    {
        bool AddOrUpdate(BasisData item);
        bool GetByName(string name, out BasisData basisData);
        bool Remove(string name);
        IEnumerable<BasisData> GetAll();
        void Save();
        void Load();
        void Shutdown();
    }
}
