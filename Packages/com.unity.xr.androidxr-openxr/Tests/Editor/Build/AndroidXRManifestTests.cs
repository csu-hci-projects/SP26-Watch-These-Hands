#if UNITY_EDITOR && UNITY_ANDROID
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor.XR.OpenXR.Features.Android;

namespace UnityEditor.XR.OpenXR.Features.Android.Tests.Build
{
    /// <summary>
    /// Class testing build helpers for Android XR
    /// </summary>
    [TestFixture]
    public class AndroidXRManifestTests
    {
        static readonly string TestManifestString =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
            + "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"\n"
            + "    package=\"com.unity.sample\"\n"
            + "    android:versionCode=\"1\"\n"
            + "    android:versionName=\"1.0\" >\n"
            + "</manifest>\n";

        static readonly string k_True = "true";
        static readonly string k_False = "false";

        static bool HasFeature(XElement manifest, string featureName, bool isRequired)
        {
            var usesFeature = manifest
                .Elements()
                .Where(
                    x => x.Name == "uses-feature" && x.Attribute(AndroidXRManifest.k_AndroidName).Value == featureName
                );
            if (isRequired)
            {
                // if omitted, the default for android:required is true
                return usesFeature.Any(
                    x => (x.Attribute(AndroidXRManifest.k_AndroidRequired)?.Value ?? k_True) == k_True
                );
            }
            return usesFeature.Any(x => x.Attribute(AndroidXRManifest.k_AndroidRequired)?.Value == k_False);
        }

        [Test]
        public void TestUsesFeatureRequiredTrue()
        {
            var manifest = XElement.Parse(TestManifestString);
            AndroidXRManifest.AddFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature);
            Assert.True(HasFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, isRequired: true));
        }

        [Test]
        public void TestUsesFeatureRequiredFalse()
        {
            var manifest = XElement.Parse(TestManifestString);
            AndroidXRManifest.AddFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, required: false);
            Assert.True(HasFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, isRequired: false));
        }

        [Test]
        public void TestUsesFeatureRequiredConflictFalseFirst()
        {
            var manifest = XElement.Parse(TestManifestString);
            AndroidXRManifest.AddFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, required: false);
            AndroidXRManifest.AddFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature);
            Assert.True(HasFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, isRequired: true));
        }

        [Test]
        public void TestUsesFeatureRequiredConflictFalseLast()
        {
            var manifest = XElement.Parse(TestManifestString);
            AndroidXRManifest.AddFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature);
            AndroidXRManifest.AddFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, required: false);
            Assert.True(HasFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, isRequired: true));
        }

        [Test]
        public void TestUsesFeatureRequiredConflictFalseFalse()
        {
            var manifest = XElement.Parse(TestManifestString);
            AndroidXRManifest.AddFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, required: false);
            AndroidXRManifest.AddFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, required: false);
            Assert.True(HasFeature(manifest, AndroidXRManifest.k_EyeTrackingHardwareFeature, isRequired: false));
        }
    }
}
#endif
