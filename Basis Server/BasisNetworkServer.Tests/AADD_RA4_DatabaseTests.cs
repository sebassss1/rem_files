using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BasisNetworkServer.BasisNetworking;
using Xunit;

namespace BasisNetworkServer.Tests
{
    public class AADD_RA4_DatabaseTests : IDisposable
    {
        private const string TestDbPath = "test_basis_data.db";

        public AADD_RA4_DatabaseTests()
        {
            // Clean up any existing test db
            if (File.Exists(TestDbPath)) File.Delete(TestDbPath);
            BasisPersistentDatabase.Instance.SetFilePath(TestDbPath);
        }

        public void Dispose()
        {
            // Final cleanup
            if (File.Exists(TestDbPath)) File.Delete(TestDbPath);
        }

        [Fact]
        public void TestNoSQLPersistenceAndRetrieval()
        {
            // AADD-RA4: Demonstrate storing complex objects in a NoSQL environment
            var payload = new ConcurrentDictionary<string, object>();
            payload.TryAdd("Level", 10);
            payload.TryAdd("Health", 100.5f);
            
            var player = new BasisData("HeroPlayer", payload);

            // Act
            BasisPersistentDatabase.AddOrUpdateStatic(player);

            // Assert
            bool found = BasisPersistentDatabase.GetByNameStatic("HeroPlayer", out var retrieved);
            Assert.True(found);
            Assert.Equal("HeroPlayer", retrieved.Name);
            Assert.Equal(10, Convert.ToInt32(retrieved.JsonPayload["Level"]));
            Assert.Equal(100.5f, Convert.ToSingle(retrieved.JsonPayload["Health"]));
        }

        [Fact]
        public void TestAdvancedQueryingWithLINQ()
        {
            // AADD-RA4: Demonstrate advanced querying capabilities
            for (int i = 1; i <= 5; i++)
            {
                var p = new ConcurrentDictionary<string, object>();
                p.TryAdd("Rank", i);
                BasisPersistentDatabase.AddOrUpdateStatic(new BasisData($"Player_{i}", p));
            }

            // Act: Find players with Rank > 3 using LINQ on the collection
            var allData = BasisPersistentDatabase.Instance.GetAll();
            var highRankers = allData.Where(d => Convert.ToInt32(d.JsonPayload["Rank"]) > 3).ToList();

            // Assert
            Assert.Equal(2, highRankers.Count);
            Assert.Contains(highRankers, r => r.Name == "Player_4");
            Assert.Contains(highRankers, r => r.Name == "Player_5");
        }

        [Fact]
        public void TestEncryptionAtRest()
        {
            // RA5: Ensure the database file is encrypted and not readable as plain text
            var payload = new ConcurrentDictionary<string, object>();
            payload.TryAdd("SecretKey", "XABC-123-Y");
            BasisPersistentDatabase.AddOrUpdateStatic(new BasisData("SecretPlayer", payload));

            // Force flush is not needed as AddOrUpdate closes the db in my implementation
            
            // Read the binary file directly
            byte[] fileBytes = File.ReadAllBytes(TestDbPath);
            string fileString = File.ReadAllText(TestDbPath); // This might be garbled but good for Contains check

            // Assert: The plain text "SecretKey" or "XABC-123-Y" should NOT be visible in the binary file
            Assert.DoesNotContain("SecretKey", fileString);
            Assert.DoesNotContain("XABC-123-Y", fileString);
        }
    }
}
