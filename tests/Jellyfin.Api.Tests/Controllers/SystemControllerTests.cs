using System;
using System.IO;
using Jellyfin.Api.Controllers;
using Jellyfin.Server.Implementations.SystemBackupService;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers
{
    public class SystemControllerTests
    {
        [Fact]
        public void GetLogFile_FileDoesNotExist_ReturnsNotFound()
        {
            var mockFileSystem = new Mock<IFileSystem>();
            mockFileSystem
                .Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns([new() { Name = "file1.txt" }, new() { Name = "file2.txt" }]);

            var controller = new SystemController(
                Mock.Of<ILogger<SystemController>>(),
                Mock.Of<IServerApplicationHost>(),
                Mock.Of<IServerApplicationPaths>(),
                mockFileSystem.Object,
                Mock.Of<INetworkManager>(),
                Mock.Of<ISystemManager>());

            var result = controller.GetLogFile("DOES_NOT_EXIST.txt");

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public void GetLogFile_FileOpenForWriting_ReturnsFile()
        {
            var logDirectory = Path.Combine(Path.GetTempPath(), "jellyfin-system-log-tests");
            Directory.CreateDirectory(logDirectory);
            var name = "log_" + Guid.NewGuid().ToString("N") + ".log";
            var path = Path.Combine(logDirectory, name);

            try
            {
                // The file logger keeps the current log open for writing and only shares read access.
                using (new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    // On Windows, directory listings keep reporting the time the log was opened
                    // until the logger closes it, so the current log can look more than an hour old.
                    var mockFileSystem = new Mock<IFileSystem>();
                    mockFileSystem
                        .Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                        .Returns([new() { Name = name, FullName = path, LastWriteTimeUtc = DateTime.UtcNow.AddHours(-2) }]);

                    var controller = new SystemController(
                        Mock.Of<ILogger<SystemController>>(),
                        Mock.Of<IServerApplicationHost>(),
                        Mock.Of<IServerApplicationPaths>(),
                        mockFileSystem.Object,
                        Mock.Of<INetworkManager>(),
                        Mock.Of<ISystemManager>());

                    var result = controller.GetLogFile(name);

                    var fileResult = Assert.IsType<FileStreamResult>(result);
                    fileResult.FileStream.Dispose();
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
