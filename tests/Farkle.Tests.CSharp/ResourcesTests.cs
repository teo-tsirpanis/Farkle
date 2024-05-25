// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using System.Globalization;
#if NET8_0_OR_GREATER
using System.Text;
#endif

namespace Farkle.Tests.CSharp;

internal class ResourcesTests
{
    [Test]
    public void TestAllGreekResourcesAreDefined()
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

// CompositeFormat was introduced in .NET 8.
#if NET8_0_OR_GREATER
    [TestCase(null)]
    [TestCase("el-GR")]
    public void TestAllStringResourcesAreValidFormatStrings(string? cultureOrInvariant)
    {
        Assert.Multiple(() =>
        {
            var culture = cultureOrInvariant is not null ? new CultureInfo(cultureOrInvariant) : CultureInfo.InvariantCulture;
            var resourceManager = Resources.ResourceManager;
            foreach (var property in typeof(Resources).GetProperties())
            {
                if (property.PropertyType != typeof(string))
                {
                    continue;
                }
                var value = resourceManager.GetString(property.Name, culture);
                if (value is null)
                {
                    continue;
                }
                Assert.That(() => CompositeFormat.Parse(value), Throws.Nothing,
                    $"String {property.Name} is not a valid format string");
            }
        });
    }
#endif
}
