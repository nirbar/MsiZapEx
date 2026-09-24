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
                    var upgrade = new UpgradeInfo(new Guid(id));
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

            ProductInfo? productInfo = null;
            Assert.DoesNotThrow(() => { productInfo = ProductInfo.RegisterDummyProduct(upgradeCode, name, version); });
            Assert.That(productInfo, Is.Not.Null);
            Assert.That(productInfo.DisplayName, Is.EqualTo(name));
            Assert.That(productInfo.DisplayVersion, Is.EqualTo(version.ToString()));

            var expectedStatus = ProductInfo.StatusFlags.Good & ~ProductInfo.StatusFlags.Components & ~ProductInfo.StatusFlags.ComponentsGood;
            Assert.That(productInfo.Status, Is.EqualTo(expectedStatus));
        }
    }
}
