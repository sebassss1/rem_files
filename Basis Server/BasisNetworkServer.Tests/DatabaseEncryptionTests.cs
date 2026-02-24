using System.Collections.Concurrent;
using BasisNetworkServer.BasisNetworking;
using BasisNetworkServer.Security;
using Xunit;

namespace BasisNetworkServer.Tests
{
    public class DatabaseEncryptionTests
    {
        [Fact]
        public void TestEncryptionDecryptionConsistency()
        {
            // Arrange
            string originalJson = "{\"Name\":\"TestUser\",\"Balance\":1000}";
            
            // Act
            string encrypted = BasisEncryptionUtility.Encrypt(originalJson);
            string decrypted = BasisEncryptionUtility.Decrypt(encrypted);
            
            // Assert
            Assert.NotEqual(originalJson, encrypted); // Ensure it's actually encrypted
            Assert.Equal(originalJson, decrypted);    // Ensure we can get back the original
        }

        [Fact]
        public void TestBasisDataEncryptionCycle()
        {
            // Arrange
            var payload = new ConcurrentDictionary<string, object>();
            payload.TryAdd("Score", 500);
            var originalData = new BasisData("PlayerOne", payload);
            
            // Act & Assert (simulating the Save/Load cycle)
            string json = BasisPersistentDatabase.Serialize(originalData);
            string encrypted = BasisEncryptionUtility.Encrypt(json);
            
            Assert.DoesNotContain("PlayerOne", encrypted); // Should not contain plain text
            Assert.DoesNotContain("500", encrypted);
            
            string decrypted = BasisEncryptionUtility.Decrypt(encrypted);
            var restoredData = BasisPersistentDatabase.Deserialize(decrypted);
            
            Assert.Equal(originalData.Name, restoredData.Name);
            Assert.Equal(500, restoredData.JsonPayload["Score"]);
        }
    }
}
