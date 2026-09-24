using Microsoft.Win32;
using MsiZapEx;

namespace MsiZapExTest
{
    public class DummyBundleTests
    {
        [TearDown]
        public void TearDown()
        {
            var guids = new List<string>(["{E817F666-9062-48DA-8440-1E58440B8670}", "{35674CC1-9967-4E74-872D-EC2928BF95D9}"]);
            foreach (string id in guids)
            {
                try
                {
                    var bis = BundleInfo.FindByUpgradeCode(new Guid(id));
                    foreach (var b in bis)
                    {
                        try
                        {
                            b.Prune();
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        [Test]
        public void CreateDummyBundleX86()
        {
            var upgradeCode = new Guid("{E817F666-9062-48DA-8440-1E58440B8670}");
            var name = nameof(CreateDummyBundleX86);
            var version = new Version(1, 2, 3, 4);
            var view = RegistryView.Registry32;

            BundleInfo? bundleInfo = null;
            Assert.DoesNotThrow(() => { bundleInfo = BundleInfo.RegisterDummyBundle(upgradeCode, name, version, view); });
            Assert.That(bundleInfo, Is.Not.Null);
            Assert.That(bundleInfo.RegistryView, Is.EqualTo(view));
            Assert.That(bundleInfo.BundleUpgradeCodes, Is.Not.Null);
            Assert.That(bundleInfo.BundleUpgradeCodes, Contains.Item(upgradeCode));
            Assert.That(bundleInfo.DisplayName, Is.EqualTo(name));
            Assert.That(bundleInfo.DisplayVersion, Is.EqualTo(version.ToString()));
            Assert.That(bundleInfo.Dependencies, Is.Empty);
            Assert.That(bundleInfo.Dependents, Is.Empty);
            Assert.That(bundleInfo.Variables, Is.Empty);

            var expectedStatus = BundleInfo.StatusFlags.Good & ~BundleInfo.StatusFlags.Cached;
            Assert.That(bundleInfo.Status, Is.EqualTo(expectedStatus));
        }

        [Test]
        public void CreateDummyBundleX64()
        {
            var upgradeCode = new Guid("{35674CC1-9967-4E74-872D-EC2928BF95D9}");
            var name = nameof(CreateDummyBundleX64);
            var version = new Version(1, 2, 3, 4);
            var view = RegistryView.Registry32;

            BundleInfo? bundleInfo = null;
            Assert.DoesNotThrow(() => { bundleInfo = BundleInfo.RegisterDummyBundle(upgradeCode, name, version, view); });
            Assert.That(bundleInfo, Is.Not.Null);
            Assert.That(bundleInfo.RegistryView, Is.EqualTo(view));
            Assert.That(bundleInfo.BundleUpgradeCodes, Is.Not.Null);
            Assert.That(bundleInfo.BundleUpgradeCodes, Contains.Item(upgradeCode));
            Assert.That(bundleInfo.DisplayName, Is.EqualTo(name));
            Assert.That(bundleInfo.DisplayVersion, Is.EqualTo(version.ToString()));
            Assert.That(bundleInfo.Dependencies, Is.Empty);
            Assert.That(bundleInfo.Dependents, Is.Empty);
            Assert.That(bundleInfo.Variables, Is.Empty);

            var expectedStatus = BundleInfo.StatusFlags.Good & ~BundleInfo.StatusFlags.Cached;
            Assert.That(bundleInfo.Status, Is.EqualTo(expectedStatus));
        }
    }
}
