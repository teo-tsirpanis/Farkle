// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Globalization;

namespace Farkle.Tests.CSharp;

internal class ResourcesTests
{
    [Test]
    public void TestAllResourcesAreDefined()
    {
        Assert.Multiple(() =>
        {
            var greek = new CultureInfo("el-GR");
            var resourceManager = Resources.ResourceManager;
            foreach (var property in typeof(Resources).GetProperties())
            {
                if (property.PropertyType != typeof(string))
                {
                    continue;
                }
                var value = resourceManager.GetString(property.Name, CultureInfo.InvariantCulture);
                Assert.That(value, Is.Not.Null.And.Not.Empty);
                var greekValue = resourceManager.GetString(property.Name, greek);
                Assert.That(greekValue, Is.Not.Null.And.Not.Empty.And.Not.EqualTo(value),
                    $"String {property.Name} is not translated to Greek.");
            }
        });
    }
}
