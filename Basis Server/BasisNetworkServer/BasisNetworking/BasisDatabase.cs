using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

namespace BasisNetworkServer.BasisNetworking
{
    public class BasisData
    {
        [BsonId]
        public string Name { get; set; }

        public ConcurrentDictionary<string, object> JsonPayload { get; set; } = new();

        public BasisData() { } // Required for LiteDB

        public BasisData(string name, ConcurrentDictionary<string, object> jsonPayload)
        {
            Name = name;
            JsonPayload = jsonPayload ?? new ConcurrentDictionary<string, object>();
        }
    }

    public class BasisPersistentDatabase : IBasisDatabase
    {
        public static BasisPersistentDatabase Instance { get; } = new();
        private static string _dbPath = "data/basis_data.db";
        private const string CollectionName = "basis_data";

        // RA5: Protect data using encryption at rest. 
        // We use LiteDB's native AES encryption with the project's master key.
        private const string DbPassword = "BasisVRSecurityKey123!@#45678901";

        static BasisPersistentDatabase()
        {
            // Configure LiteDB to handle ConcurrentDictionary like a normal Dictionary
            BsonMapper.Global.RegisterType<ConcurrentDictionary<string, object>>(
                serialize: (dict) => new BsonDocument(dict.ToDictionary(kvp => kvp.Key, kvp => BsonMapper.Global.Serialize(kvp.Value))),
                deserialize: (bson) => new ConcurrentDictionary<string, object>(bson.AsDocument.ToDictionary(kvp => kvp.Key, kvp => BsonMapper.Global.Deserialize<object>(kvp.Value)))
            );
        }

        public void SetFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            _dbPath = path;
        }

        public bool AddOrUpdate(BasisData item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(item.Name)) return false;

            using var db = GetDatabase();
            var col = db.GetCollection<BasisData>(CollectionName);
            col.Upsert(item);
            return true;
        }

        // Static wrappers for legacy compatibility
        public static bool AddOrUpdateStatic(BasisData item) => Instance.AddOrUpdate(item);
        public static bool GetByNameStatic(string name, out BasisData basisData) => Instance.GetByName(name, out basisData);

        public static string Serialize(object obj)
        {
            using var ms = new MemoryStream();
            var serializer = new DataContractJsonSerializer(obj.GetType(), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });
            serializer.WriteObject(ms, obj);
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        public static T Deserialize<T>(string json)
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });
            return (T)serializer.ReadObject(ms);
        }

        public static BasisData Deserialize(string json) => Deserialize<BasisData>(json);

        public IEnumerable<BasisData> GetAll()
        {
            using var db = GetDatabase();
            var col = db.GetCollection<BasisData>(CollectionName);
            return col.FindAll().ToList();
        }

        public bool GetByName(string name, out BasisData basisData)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                basisData = null;
                return false;
            }
            using var db = GetDatabase();
            var col = db.GetCollection<BasisData>(CollectionName);
            basisData = col.FindById(name);
            return basisData != null;
        }

        public bool Remove(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            using var db = GetDatabase();
            var col = db.GetCollection<BasisData>(CollectionName);
            return col.Delete(name);
        }

        private LiteDatabase GetDatabase()
        {
            // Ensure directory exists for persistence
            var directory = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // AADD-RA4: Utilazing a NoSQL Document-Oriented Database.
            return new LiteDatabase($"Filename={_dbPath};Password={DbPassword}");
        }

        public void Save() { /* LiteDB saves automatically on transaction/disposal */ }
        public void Load() { /* LiteDB loads on demand */ }
        public void Shutdown() { /* No manual shutdown needed for LiteDB instance here */ }

        public static void ShutdownStatic() => Instance.Shutdown();
    }
}
