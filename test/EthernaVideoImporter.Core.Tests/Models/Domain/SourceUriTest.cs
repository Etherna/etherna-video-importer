using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Etherna.VideoImporter.Core.Models.Domain
{
    public class SourceUriTest
    {
        // Internal classes.
        public class GetParentDirectoryAsAbsoluteUriTestElement
        {
            public GetParentDirectoryAsAbsoluteUriTestElement(
                SourceUri sourceUri,
                SourceUriKind allowedUriKinds,
                string? baseDirectory,
                (string, SourceUriKind)? expectedResult)
            {
                SourceUri = sourceUri;
                AllowedUriKinds = allowedUriKinds;
                BaseDirectory = baseDirectory;
                ExpectedResult = expectedResult;
            }

            public SourceUri SourceUri { get; }
            public SourceUriKind AllowedUriKinds { get; }
            public string? BaseDirectory { get; }
            public (string, SourceUriKind)? ExpectedResult { get; }
        }

        public class ToAbsoluteUriTestElement
        {
            public ToAbsoluteUriTestElement(
                SourceUri sourceUri,
                SourceUriKind allowedUriKinds,
                string? baseDirectory,
                (string, SourceUriKind) expectedResult,
                bool appendExpectedToCurrentDirectory = false)
            {
                SourceUri = sourceUri;
                AllowedUriKinds = allowedUriKinds;
                BaseDirectory = baseDirectory;
                ExpectedResult = expectedResult;
                AppendExpectedToCurrentDirectory = appendExpectedToCurrentDirectory;
            }

            public ToAbsoluteUriTestElement(
                SourceUri sourceUri,
                SourceUriKind allowedUriKinds,
                string? baseDirectory,
                Type expectedExceptionType)
            {
                SourceUri = sourceUri;
                AllowedUriKinds = allowedUriKinds;
                BaseDirectory = baseDirectory;
                ExpectedExceptionType = expectedExceptionType;
            }

            public SourceUri SourceUri { get; }
            public SourceUriKind AllowedUriKinds { get; }
            public string? BaseDirectory { get; }
            public (string, SourceUriKind)? ExpectedResult { get; }
            public bool AppendExpectedToCurrentDirectory { get; }
            public Type? ExpectedExceptionType { get; }
        }

        // Data.
        public static IEnumerable<object[]> GetParentDirectoryAsAbsoluteUriTests
        {
            get
            {
                var tests = new List<GetParentDirectoryAsAbsoluteUriTestElement>
                {
                };

                return tests.Select(t => new object[] { t });
            }
        }

        public static IEnumerable<object[]> ToAbsoluteUriTests
        {
            get
            {
                var tests = new List<ToAbsoluteUriTestElement>
                {
                    //local absolute (or online relative) without restrictions. Throws exception because is ambiguous
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"),
                        SourceUriKind.All,
                        null,
                        typeof(InvalidOperationException)),

                    //local absolute (or online relative) with local restriction
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"),
                        SourceUriKind.Local,
                        null,
                        ("/test", SourceUriKind.LocalAbsolute)),

                    //local absolute (or online relative) with online restriction. Throws exception because base directory is null
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"),
                        SourceUriKind.Online,
                        null,
                        typeof(InvalidOperationException)),
                    
                    //local absolute (or online relative) with local base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"),
                        SourceUriKind.All,
                        "/absolute/local",
                        ("/test", SourceUriKind.LocalAbsolute)),

                    //local absolute (or online relative) with online base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"),
                        SourceUriKind.All,
                        "https://example.com/",
                        ("https://example.com/test", SourceUriKind.OnlineAbsolute)),

                    //local absolute (or online relative) with relative base directory. Throws exception because is ambiguous and base directory is wrong
                    new ToAbsoluteUriTestElement(
                        new SourceUri("/test"),
                        SourceUriKind.All,
                        "not/absolute",
                        typeof(InvalidOperationException)),
                    
                    //local relative (or online relative) without restrictions. Throws exception because is ambiguous
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        null,
                        typeof(InvalidOperationException)),

                    //local relative (or online relative) with local restriction
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.Local,
                        null,
                        ("test", SourceUriKind.LocalAbsolute),
                        true),

                    //local relative (or online relative) with online restriction. Throws exception because base directory is null
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.Online,
                        null,
                        typeof(InvalidOperationException)),
                    
                    //local relative (or online relative) with local base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        Path.GetFullPath("/absolute/local"),
                        (Path.GetFullPath("/absolute/local/test"), SourceUriKind.LocalAbsolute)),

                    //local relative (or online relative) with online base directory
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        "https://example.com/",
                        ("https://example.com/test", SourceUriKind.OnlineAbsolute)),

                    //local relative (or online relative) with relative base directory. Throws exception because is ambiguous and base directory is wrong
                    new ToAbsoluteUriTestElement(
                        new SourceUri("test"),
                        SourceUriKind.All,
                        "not/absolute",
                        typeof(InvalidOperationException)),
                    
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
                        typeof(InvalidOperationException)),

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
                        "/absolute/local",
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

        // Tests.
        [Theory]
        [InlineData("test", SourceUriKind.All, "test", SourceUriKind.Relative)]
        [InlineData("test", SourceUriKind.Local, "test", SourceUriKind.LocalRelative)]
        [InlineData("https://example.com/", SourceUriKind.All, "https://example.com/", SourceUriKind.OnlineAbsolute)]
        public void ConstructorInitializeProperties(string uri, SourceUriKind allowedUriKinds, string expectedOriginalUri, SourceUriKind expectedUriKind)
        {
            var sourceUri = new SourceUri(uri, allowedUriKinds);

            Assert.Equal(expectedOriginalUri, sourceUri.OriginalUri);
            Assert.Equal(expectedUriKind, sourceUri.UriKind);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public void EmptyUriThrowsException(string uri)
        {
            Assert.Throws<ArgumentException>(() => new SourceUri(uri));
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
            if (test.ExpectedResult is not null)
            {
                var result = test.SourceUri.ToAbsoluteUri(
                    test.AllowedUriKinds,
                    test.BaseDirectory);

                var expectedResult = test.AppendExpectedToCurrentDirectory ?
                    (Path.Combine(Directory.GetCurrentDirectory(), test.ExpectedResult.Value.Item1), test.ExpectedResult.Value.Item2) :
                    test.ExpectedResult;

                Assert.Equal(expectedResult, result);
            }
            else
            {
                Assert.Throws(test.ExpectedExceptionType!,
                    () => test.SourceUri.ToAbsoluteUri(
                        test.AllowedUriKinds,
                        test.BaseDirectory));
            }
        }

        //[Theory, MemberData(nameof(GetParentDirectoryAsAbsoluteUriTests))]
        //public void TryGetParentDirectoryAsAbsoluteUri(GetParentDirectoryAsAbsoluteUriTestElement test)
        //{
        //    var result = test.SourceUri.TryGetParentDirectoryAsAbsoluteUri(
        //        test.AllowedUriKinds,
        //        test.BaseDirectory);

        //    Assert.Equal(test.ExpectedResult, result);
        //}

        [Theory]
        [InlineData("", SourceUriKind.None)]
        [InlineData("test.txt", SourceUriKind.Relative)]
        [InlineData("dir/test.txt", SourceUriKind.Relative)]
        [InlineData("dir\\test.txt", SourceUriKind.Relative)]
        [InlineData("/test.txt", SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative)]
        [InlineData("\\test.txt", SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative)]
        [InlineData("C:/dir/", SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative)]
        [InlineData("C:\\dir\\", SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative)]
        [InlineData("C:\\dir/file.txt", SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative)]
        [InlineData("/dir/", SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative)]
        [InlineData("\\dir\\", SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative)]
        [InlineData("\\dir/file.txt", SourceUriKind.LocalAbsolute | SourceUriKind.OnlineRelative)]
        [InlineData("http://example.com/file.txt", SourceUriKind.OnlineAbsolute)]
        [InlineData("https://example.com", SourceUriKind.OnlineAbsolute)]
        public void GetUriKind(string uri, SourceUriKind expectedUriKind)
        {
            var result = SourceUri.GetUriKind(uri);

            Assert.Equal(expectedUriKind, result);
        }
    }
}
