// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

namespace Farkle.Tests.CSharp;

internal class ResourcesTests
{
    [Test]
    public void TestAllResourcesAreDefined()
    {
        Assert.Multiple(() =>
        {
            foreach (var property in typeof(Resources).GetProperties())
            {
                if (property.PropertyType != typeof(string))
                {
                    continue;
                }
                var value = property.GetValue(null);
                Assert.That(value, Is.Not.Null.And.Not.Empty);
            }
        });
    }
}
