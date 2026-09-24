using Microsoft.Win32;
using MsiZapEx;

namespace MsiZapExTest
{
    public class DummyProductTests
    {
        [TearDown]
        public void TearDown()
        {
            var guids = new List<string>(["{10E79D07-D6E7-4A79-B9CC-1A87D29D41C9}"]);
            foreach (string id in guids)
            {
                try
                {
                    var upgrade = new UpgradeInfo(new Guid(id), true);
                    foreach (var p in upgrade.RelatedProducts)
                    {
                        try
                        {
                            upgrade.Prune(p.ProductCode);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        [Test]
        public void CreateDummyProduct()
        {
            var upgradeCode = new Guid("{10E79D07-D6E7-4A79-B9CC-1A87D29D41C9}");
            var name = nameof(CreateDummyProduct);
            var version = new Version(1, 2, 3, 4);

            UpgradeInfo? upgradeInfo = null;
            Assert.DoesNotThrow(() => { upgradeInfo = UpgradeInfo.RegisterDummyProduct(upgradeCode, name, version); });
            Assert.That(upgradeInfo, Is.Not.Null);
            Assert.That(upgradeInfo.RelatedProducts, Is.Not.Null);
            Assert.That(upgradeInfo.RelatedProducts.Count, Is.EqualTo(1));

            var expectedBundleStatus = UpgradeInfo.StatusFlags.Good & ~UpgradeInfo.StatusFlags.ProductsGood;
            Assert.That(upgradeInfo.Status, Is.EqualTo(expectedBundleStatus));

            var msi = upgradeInfo.RelatedProducts.First();
            Assert.That(msi.DisplayName, Is.EqualTo(name));
            Assert.That(msi.DisplayVersion, Is.EqualTo(version.ToString()));

            var expectedMsiStatus = ProductInfo.StatusFlags.Good & ~ProductInfo.StatusFlags.Components & ~ProductInfo.StatusFlags.ComponentsGood;
            Assert.That(msi.Status, Is.EqualTo(expectedMsiStatus));
        }
    }
}
