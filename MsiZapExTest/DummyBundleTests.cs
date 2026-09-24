using Microsoft.Win32;
using MsiZapEx;
using System.Diagnostics;

namespace MsiZapExTest
{
    public class DummyBundleTests
    {
        [TearDown]
        public void TearDown()
        {
            var guids = new List<string>(["{E817F666-9062-48DA-8440-1E58440B8670}", "{35674CC1-9967-4E74-872D-EC2928BF95D9}", "{d01a7a13-e3f1-457f-ba5a-2a8819942bfd}"]);
            foreach (string id in guids)
            {
                try
                {
                    var bis = BundleInfo.FindByUpgradeCode(new Guid(id));
                    foreach (var b in bis)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(b.BundleCachePath) && File.Exists(b.BundleCachePath))
                            {
                                using (var prc = Process.Start(b.BundleCachePath, "/uninstall /silent /norestart"))
                                {
                                    prc.WaitForExit(60 * 60 * 1000);
                                }
                            }
                            else
                            {
                                b.Prune();
                            }
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
            var name = TestContext.CurrentContext.Test.MethodName;
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
            var name = TestContext.CurrentContext.Test.MethodName;
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
        public void RelatedBundleTest()
        {
            var testBundlePath = Environment.GetEnvironmentVariable("TestBundlePath");
            if (string.IsNullOrEmpty("TestBundlePath"))
            {
                Assert.Ignore("Provide `TestBundlePath` parameter with the full path of the test bundle to run this test");
            }
            Assert.That(testBundlePath, Does.Exist);

            var upgradeCode = new Guid("{d01a7a13-e3f1-457f-ba5a-2a8819942bfd}");
            var name = TestContext.CurrentContext.Test.MethodName;
            var version = new Version(0, 1);
            var view = RegistryView.Registry64;

            BundleInfo? bundleInfo = null;
            Assert.DoesNotThrow(() => { bundleInfo = BundleInfo.RegisterDummyBundle(upgradeCode, name, version, view); });
            Assert.That(bundleInfo, Is.Not.Null);
            var bundleProductCode = bundleInfo.BundleProductCode;

            var logFile = Path.Combine(TestContext.CurrentContext.WorkDirectory, Path.GetFileNameWithoutExtension(testBundlePath) + TestContext.CurrentContext.Test.MethodName + ".log");
            using (var prc = Process.Start(testBundlePath, $"/silent /install /norestart /log \"{logFile}\""))
            {
                prc.WaitForExit(60 * 60 * 1000);
                Assert.That(prc.ExitCode, Is.EqualTo(0));
            }

            Assert.That(logFile, Does.Exist);
            var logText = File.ReadAllText(logFile);
            var expected = string.Format("Detected related bundle: {0}, type: Upgrade, scope: PerMachine, version: {1}, cached: No", bundleProductCode.ToString("B"), version);
            Assert.That(logText, Does.Contain(expected));
        }
    }
}
