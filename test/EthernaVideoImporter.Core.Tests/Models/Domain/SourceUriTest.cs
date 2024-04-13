//   Copyright 2022-present Etherna SA
// 
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
// 
//       http://www.apache.org/licenses/LICENSE-2.0
// 
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class SourceUriTest
    {
        // Classes.
        public class ToAbsoluteUriTestElement
        {
            public ToAbsoluteUriTestElement(
                SourceUri sourceUri,
                SourceUriKind allowedUriKinds,
                string? baseDirectory,
                (string, SourceUriKind)? expectedResult = null,
                Type? expectedExceptionType = null)
            {
                SourceUri = sourceUri;
                AllowedUriKinds = allowedUriKinds;
                BaseDirectory = baseDirectory;
                ExpectedResult = expectedResult;
                ExpectedExceptionType = expectedExceptionType;
            }

            public SourceUri SourceUri { get; }
            public SourceUriKind AllowedUriKinds { get; }
            public string? BaseDirectory { get; }
            public (string, SourceUriKind)? ExpectedResult { get; }
            public Type? ExpectedExceptionType { get; }
        }

        public class ToAbsoluteUriUsesBaseDirectoryTestElement
        {
            public ToAbsoluteUriUsesBaseDirectoryTestElement(
                SourceUri sourceUri,
                string? argBaseDirectory,
                (string, SourceUriKind) expectedResult)
            {
                SourceUri = sourceUri;
                ArgBaseDirectory = argBaseDirectory;
                ExpectedResult = expectedResult;
            }

            public ToAbsoluteUriUsesBaseDirectoryTestElement(
                SourceUri sourceUri,
                string? argBaseDirectory,
                Type expectedExceptionType)
            {
                SourceUri = sourceUri;
                ArgBaseDirectory = argBaseDirectory;
                ExpectedExceptionType = expectedExceptionType;
            }

            public SourceUri SourceUri { get; }
            public string? ArgBaseDirectory { get; }
            public (string, SourceUriKind)? ExpectedResult { get; }
            public Type? ExpectedExceptionType { get; }
        }

        public class ToAbsoluteUriUsesAllowedUriKindsTestElement
        {
            public ToAbsoluteUriUsesAllowedUriKindsTestElement(
                SourceUri sourceUri,
                SourceUriKind argAllowedUriKinds,
                (string, SourceUriKind) expectedResult)
            {
                SourceUri = sourceUri;
                ArgAllowedUriKinds = argAllowedUriKinds;
                ExpectedResult = expectedResult;
            }

            public ToAbsoluteUriUsesAllowedUriKindsTestElement(
                SourceUri sourceUri,
                SourceUriKind argAllowedUriKinds,
                Type expectedExceptionType)
            {
                SourceUri = sourceUri;
                ArgAllowedUriKinds = argAllowedUriKinds;
                ExpectedExceptionType = expectedExceptionType;
            }

            public SourceUri SourceUri { get; }
            public SourceUriKind ArgAllowedUriKinds { get; }
            public (string, SourceUriKind)? ExpectedResult { get; }
            public Type? ExpectedExceptionType { get; }
        }

        public class TryGetParentDirectoryAsAbsoluteUriTestElement
        {
            public TryGetParentDirectoryAsAbsoluteUriTestElement(
                SourceUri sourceUri,
                (string, SourceUriKind)? expectedResult)
            {
                SourceUri = sourceUri;
                ExpectedResult = expectedResult;
            }

            public TryGetParentDirectoryAsAbsoluteUriTestElement(
                SourceUri sourceUri,
                Type expectedExceptionType)
            {
                SourceUri = sourceUri;
                ExpectedExceptionType = expectedExceptionType;
            }

            public SourceUri SourceUri { get; }
            public (string, SourceUriKind)? ExpectedResult { get; }
            public Type? ExpectedExceptionType { get; }
        }

        public class GetUriKindTestElement
        {
            public GetUriKindTestElement(
                string uri,
                SourceUriKind expectedUriKind)
            {
                Uri = uri;
                ExpectedUriKind = expectedUriKind;
            }

            public string Uri { get; }
            public SourceUriKind ExpectedUriKind { get; }
        }

        // Data.
        public static IEnumerable<object[]> ToAbsoluteUriTests
        {
            get
            {
                var tests = new List<ToAbsoluteUriTestElement>
                {
                    //local absolute (or online relative) without restrictions. Throws exception because is ambiguous
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"), //unix-like
                        SourceUriKind.All,
                        null,
                        expectedExceptionType: typeof(InvalidOperationException)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("D:\\test"), //windows-like
                        SourceUriKind.All,
                        null,
                        expectedExceptionType: typeof(InvalidOperationException)),

                    //local absolute (or online relative) with local restriction
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"), //unix-like
                        SourceUriKind.Local,
                        null,
                        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory())!, "test") : //ex: "C:\\test"
                            "/test", SourceUriKind.LocalAbsolute)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("D:\\test"), //windows-like
                        SourceUriKind.Local,
                        null,
                        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            "D:\\test" :
                            Path.Combine(Directory.GetCurrentDirectory(), "D:\\test"), SourceUriKind.LocalAbsolute)),

                    //local absolute (or online relative) with online restriction. Throws exception because base directory is null
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"), //unix-like
                        SourceUriKind.Online,
                        null,
                        expectedExceptionType: typeof(InvalidOperationException)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("D:\\test"), //windows-like
                        SourceUriKind.Online,
                        null,
                        expectedExceptionType: typeof(InvalidOperationException)),
                    
                    //local absolute (or online relative) with local base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"), //unix-like
                        SourceUriKind.All,
                        "/absolute/local", //unix-like
                        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory())!, "test") : //ex: "C:\\test"
                            "/test", SourceUriKind.LocalAbsolute)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("D:\\test"), //windows-like
                        SourceUriKind.All,
                        "/absolute/local", //unix-like
                        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            "D:\\test" :
                            "/absolute/local/D:\\test",
                         SourceUriKind.LocalAbsolute)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"), //unix-like
                        SourceUriKind.All, //with no restrictions
                        "E:\\absolute\\local", //windows-like
                        expectedResult: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            ("E:\\test", SourceUriKind.LocalAbsolute) :
                            null,
                        expectedExceptionType: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            null :
                            typeof(InvalidOperationException)), //throws exception because is ambiguous and base directory is not absolute

                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"), //unix-like
                        SourceUriKind.Local, //with local restriction
                        "E:\\absolute\\local", //windows-like
                        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            "E:\\test" :
                            "/test", SourceUriKind.LocalAbsolute)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("D:\\test"), //windows-like
                        SourceUriKind.All,
                        "E:\\absolute\\local", //windows-like
                        expectedResult: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            ("D:\\test", SourceUriKind.LocalAbsolute) :
                            null,
                        expectedExceptionType: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            null :
                            typeof(InvalidOperationException)), //throws exception because is ambiguous, and anyway base directory is not absolute

                    new ToAbsoluteUriTestElement(
                        new SourceUri("D:\\test"), //unix-like
                        SourceUriKind.Local, //with local restriction
                        "E:\\absolute\\local", //windows-like
                        expectedResult: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            ("D:\\test", SourceUriKind.LocalAbsolute) :
                            null,
                        expectedExceptionType: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            null :
                            typeof(InvalidOperationException)), //throws exception because base directory is not absolute

                    //local absolute (or online relative) with online base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"), //unix-like
                        SourceUriKind.All,
                        "https://example.com/dir/",
                        ("https://example.com/test", SourceUriKind.OnlineAbsolute)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("D:\\test"), //windows-like
                        SourceUriKind.All,
                        "https://example.com/dir/",
                        ("https://example.com/dir/D%3A/test", SourceUriKind.OnlineAbsolute)),

                    //local absolute (or online relative) with relative base directory. Throws exception because is ambiguous and base directory is not absolute
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"), //unix-like
                        SourceUriKind.All,
                        "not/absolute",
                        expectedExceptionType: typeof(InvalidOperationException)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("D:\\test"), //windows-like
                        SourceUriKind.All,
                        "not/absolute",
                        expectedExceptionType: typeof(InvalidOperationException)),
                    
                    //local relative (or online relative) without restrictions. Throws exception because is ambiguous
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        null,
                        expectedExceptionType: typeof(InvalidOperationException)),

                    //local relative (or online relative) with local restriction
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.Local,
                        null,
                        (Path.Combine(Directory.GetCurrentDirectory(), "test"), SourceUriKind.LocalAbsolute)),

                    //local relative (or online relative) with online restriction. Throws exception because base directory is null
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.Online,
                        null,
                        expectedExceptionType: typeof(InvalidOperationException)),
                    
                    //local relative (or online relative) with local base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        "/absolute/local", //unix-like
                        (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory())!, "absolute\\local\\test") :
                            "/absolute/local/test", SourceUriKind.LocalAbsolute)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        "D:\\absolute\\local", //windows-like
                        expectedResult: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            ("D:\\absolute\\local\\test", SourceUriKind.LocalAbsolute) :
                            null,
                        expectedExceptionType: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            null :
                            typeof(InvalidOperationException)), //throws exception because is ambiguous, and anyway base directory is not absolute

                    //local relative (or online relative) with online base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        "https://example.com/dir/",
                        ("https://example.com/dir/test", SourceUriKind.OnlineAbsolute)),

                    //local relative (or online relative) with relative base directory. Throws exception because is ambiguous and base directory is not absolute
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        "not/absolute",
                        expectedExceptionType: typeof(InvalidOperationException)),
                    
                    //online absolute without restrictions
                    new ToAbsoluteUriTestElement(
                        new SourceUri("https://example.com/"),
                        SourceUriKind.All,
                        null,
                        ("https://example.com/", SourceUriKind.OnlineAbsolute)),

                    //online absolute with local restriction
                    new ToAbsoluteUriTestElement(
                        new SourceUri("https://example.com/"),
                        SourceUriKind.Local,
                        null,
                        expectedExceptionType: typeof(InvalidOperationException)),

                    //online absolute with online restriction
                    new ToAbsoluteUriTestElement(
                        new SourceUri("https://example.com/"),
                        SourceUriKind.Online,
                        null,
                        ("https://example.com/", SourceUriKind.OnlineAbsolute)),
                    
                    //online absolute with local base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("https://example.com/"),
                        SourceUriKind.All,
                        "/absolute/local", //unix-like
                        ("https://example.com/", SourceUriKind.OnlineAbsolute)),

                    new ToAbsoluteUriTestElement(
                        new SourceUri("https://example.com/"),
                        SourceUriKind.All,
                        "C:\\absolute\\local", //windows-like
                        ("https://example.com/", SourceUriKind.OnlineAbsolute)),

                    //online absolute with online base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("https://example.com/"),
                        SourceUriKind.All,
                        "https://other-site.com/",
                        ("https://example.com/", SourceUriKind.OnlineAbsolute)),

                    //online absolute with relative base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("https://example.com/"),
                        SourceUriKind.All,
                        "not/absolute",
                        ("https://example.com/", SourceUriKind.OnlineAbsolute)),
                };

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> ToAbsoluteUriUsesBaseDirectoryTests
        {
            get
            {
                var tests = new List<ToAbsoluteUriUsesBaseDirectoryTestElement>
                {
                    //null constructor, null method
                    new ToAbsoluteUriUsesBaseDirectoryTestElement(
                        new SourceUri("test"),
                        null,
                        typeof(InvalidOperationException)),

                    //set constructor, null method
                    new ToAbsoluteUriUsesBaseDirectoryTestElement(
                        new SourceUri("test",
                            defaultBaseDirectory: "https://constructor.com"),
                        null,
                        ("https://constructor.com/test", SourceUriKind.OnlineAbsolute)),

                    //null constructor, set method
                    new ToAbsoluteUriUsesBaseDirectoryTestElement(
                        new SourceUri("test"),
                        "https://method.com",
                        ("https://method.com/test", SourceUriKind.OnlineAbsolute)),

                    //set constructor, set method
                    new ToAbsoluteUriUsesBaseDirectoryTestElement(
                        new SourceUri("test",
                            defaultBaseDirectory: "https://constructor.com"),
                        "https://method.com",
                        ("https://method.com/test", SourceUriKind.OnlineAbsolute))
                };

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> ToAbsoluteUriUsesAllowedUriKindsTests
        {
            get
            {
                var tests = new List<ToAbsoluteUriUsesAllowedUriKindsTestElement>
                {
                    //all constructor, all method.
                    new ToAbsoluteUriUsesAllowedUriKindsTestElement(
                        new SourceUri("test"), //UriKind == Urikinds.Relative
                        SourceUriKind.All,
                        typeof(InvalidOperationException)), //throws exception because is ambiguous

                    //limit constructor, all method
                    new ToAbsoluteUriUsesAllowedUriKindsTestElement(
                        new SourceUri("test", SourceUriKind.Local), //UriKind == Urikinds.LocalRelative
                        SourceUriKind.All,
                        (Path.GetFullPath("test"), SourceUriKind.LocalAbsolute)),

                    //all constructor, limit method
                    new ToAbsoluteUriUsesAllowedUriKindsTestElement(
                        new SourceUri("test"), //UriKind == Urikinds.Relative
                        SourceUriKind.Local,
                        (Path.GetFullPath("test"), SourceUriKind.LocalAbsolute)),
                    
                    //limit constructor, limit method
                    new ToAbsoluteUriUsesAllowedUriKindsTestElement(
                        new SourceUri("test", SourceUriKind.Local), //UriKind == Urikinds.LocalRelative
                        SourceUriKind.Online,
                        typeof(InvalidOperationException)), //throws exception because can't find a valid uri type
                };

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> TryGetParentDirectoryAsAbsoluteUriTests
        {
            get
            {
                var tests = new List<TryGetParentDirectoryAsAbsoluteUriTestElement>
                {
                    //local without parent
                    new TryGetParentDirectoryAsAbsoluteUriTestElement(
                        new SourceUri("/", SourceUriKind.Local),
                        ((string, SourceUriKind)?)null),

                    //local with parent
                    new TryGetParentDirectoryAsAbsoluteUriTestElement(
                        new SourceUri("parent/test", SourceUriKind.Local),
                        (Path.GetFullPath("parent"), SourceUriKind.LocalAbsolute)),

                    //online without parent
                    new TryGetParentDirectoryAsAbsoluteUriTestElement(
                        new SourceUri("https://example.com"),
                        ((string, SourceUriKind)?)null),

                    new TryGetParentDirectoryAsAbsoluteUriTestElement(
                        new SourceUri("https://example.com/"),
                        ((string, SourceUriKind)?)null),

                    //online with parent
                    new TryGetParentDirectoryAsAbsoluteUriTestElement(
                        new SourceUri("https://example.com/test"),
                        ("https://example.com/", SourceUriKind.OnlineAbsolute)),

                    new TryGetParentDirectoryAsAbsoluteUriTestElement(
                        new SourceUri("https://example.com/test/"),
                        ("https://example.com/", SourceUriKind.OnlineAbsolute)),

                    new TryGetParentDirectoryAsAbsoluteUriTestElement(
                        new SourceUri("https://example.com/parent/test"),
                        ("https://example.com/parent/", SourceUriKind.OnlineAbsolute)),

                    //exception because of invalid absolute uri
                    new TryGetParentDirectoryAsAbsoluteUriTestElement(
                        new SourceUri("test"),
                        typeof(InvalidOperationException)), //throws exception because can't resolve absolute uri
                };

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> GetUriKindTests
        {
            get
            {
                var tests = new List<GetUriKindTestElement>
                {
                    new GetUriKindTestElement(
                        "",
                        SourceUriKind.None),

                    new GetUriKindTestElement(
                        "test.txt",
                        SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "dir/test.txt",
                        SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "dir\\test.txt",
                        SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "/test.txt",
                        SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative),

                    new GetUriKindTestElement(
                        "\\test.txt",
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative :
                            SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "C:/dir/",
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative :
                            SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "C:\\dir\\",
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative :
                            SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "C:\\dir/file.txt",
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative :
                            SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "/dir/",
                        SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative),

                    new GetUriKindTestElement(
                        "\\dir\\",
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative :
                            SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "\\dir/file.txt",
                        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? //different behavior on windows host
                            SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative :
                            SourceUriKind.Relative),

                    new GetUriKindTestElement(
                        "https://example.com",
                        SourceUriKind.OnlineAbsolute),

                    new GetUriKindTestElement(
                        "https://example.com/dir/",
                        SourceUriKind.OnlineAbsolute),

                    new GetUriKindTestElement(
                        "http://example.com/dir/file.txt",
                        SourceUriKind.OnlineAbsolute),
                };

                return tests.Select(t => new object[] { t });
            }
        }

        // Tests.
        [Theory]
        [InlineData("test", SourceUriKind.All, "test", SourceUriKind.Relative, null)]
        [InlineData("test", SourceUriKind.Local, "test", SourceUriKind.LocalRelative, null)]
        [InlineData("https://example.com/", SourceUriKind.All, "https://example.com/", SourceUriKind.OnlineAbsolute, null)]
        [InlineData("test", SourceUriKind.Absolute, null, null, typeof(ArgumentException))] // throws because "test" is relative
        public void ConstructorEvaluateProperties(
            string uri,
            SourceUriKind allowedUriKinds,
            string? expectedOriginalUri,
            SourceUriKind? expectedUriKind,
            Type? expectedExceptionType)
        {
            if (expectedExceptionType is null)
            {
                var sourceUri = new SourceUri(uri, allowedUriKinds);

                Assert.Equal(expectedOriginalUri, sourceUri.OriginalUri);
                Assert.Equal(expectedUriKind, sourceUri.UriKind);
            }
            else
            {
                Assert.Throws(expectedExceptionType, () => new SourceUri(uri, allowedUriKinds));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public void EmptyUriThrowsException(string? uri)
        {
            Assert.Throws<ArgumentException>(() => new SourceUri(uri!));
        }

        [Theory]
        [InlineData("https://example.com/", SourceUriKind.None)]
        [InlineData("https://example.com/", SourceUriKind.Local)]
        public void TooRestrictiveUriKindThrowsException(string uri, SourceUriKind allowedUriKinds)
        {
            Assert.Throws<ArgumentException>(() => new SourceUri(uri, allowedUriKinds));
        }

        [Theory, MemberData(nameof(ToAbsoluteUriTests))]
        public void ToAbsoluteUri(ToAbsoluteUriTestElement test)
        {
            if (test.ExpectedExceptionType is null)
            {
                var result = test.SourceUri.ToAbsoluteUri(
                    test.AllowedUriKinds,
                    test.BaseDirectory);

                Assert.Equal(test.ExpectedResult, result);
            }
            else
            {
                Assert.Throws(test.ExpectedExceptionType,
                    () => test.SourceUri.ToAbsoluteUri(
                        test.AllowedUriKinds,
                        test.BaseDirectory));
            }
        }

        [Theory, MemberData(nameof(ToAbsoluteUriUsesBaseDirectoryTests))]
        public void ToAbsoluteUriUsesBaseDirectory(ToAbsoluteUriUsesBaseDirectoryTestElement test)
        {
            if (test.ExpectedResult is not null)
            {
                var result = test.SourceUri.ToAbsoluteUri(
                    baseDirectory: test.ArgBaseDirectory);

                Assert.Equal(test.ExpectedResult, result);
            }
            else
            {
                Assert.Throws(test.ExpectedExceptionType!,
                    () => test.SourceUri.ToAbsoluteUri(
                        baseDirectory: test.ArgBaseDirectory));
            }
        }

        [Theory, MemberData(nameof(ToAbsoluteUriUsesAllowedUriKindsTests))]
        public void ToAbsoluteUriUsesAllowedUriKinds(ToAbsoluteUriUsesAllowedUriKindsTestElement test)
        {
            if (test.ExpectedResult is not null)
            {
                var result = test.SourceUri.ToAbsoluteUri(
                    test.ArgAllowedUriKinds);

                Assert.Equal(test.ExpectedResult, result);
            }
            else
            {
                Assert.Throws(test.ExpectedExceptionType!,
                    () => test.SourceUri.ToAbsoluteUri(
                        test.ArgAllowedUriKinds));
            }
        }

        [Theory, MemberData(nameof(TryGetParentDirectoryAsAbsoluteUriTests))]
        public void TryGetParentDirectoryAsAbsoluteUri(TryGetParentDirectoryAsAbsoluteUriTestElement test)
        {
            if (test.ExpectedExceptionType is null)
            {
                var result = test.SourceUri.TryGetParentDirectoryAsAbsoluteUri();

                Assert.Equal(test.ExpectedResult, result);
            }
            else
            {
                Assert.Throws(test.ExpectedExceptionType!,
                    () => test.SourceUri.TryGetParentDirectoryAsAbsoluteUri());
            }
        }

        [Theory, MemberData(nameof(GetUriKindTests))]
        public void GetUriKind(GetUriKindTestElement test)
        {
            var result = SourceUri.GetUriKind(test.Uri);

            Assert.Equal(test.ExpectedUriKind, result);
        }
    }
}
