using BusinessLayer.Services;
using NUnit.Framework;

namespace Backend_Tool.Tests;

public sealed class PasswordHashingTests
{
    [Test]
    public void Hash_and_verify_round_trip()
    {
        var password = "UnitTestPassword_123!";
        var hash = PasswordHashing.Hash(password);

        Assert.AreNotEqual(password, hash);
        Assert.IsTrue(PasswordHashing.Verify(password, hash));
        Assert.IsFalse(PasswordHashing.Verify("wrong-password", hash));
    }
}
