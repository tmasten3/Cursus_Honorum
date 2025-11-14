using System;
using System.IO;
using System.Text;
using Game.Core.Save;
using Game.Data.Characters;
using NUnit.Framework;

namespace CursusHonorum.Tests.Runtime
{
    public class SaveRepositoryTests
    {
        private string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "CursusHonorum_SaveRepositoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrEmpty(tempRoot))
                return;

            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, true);
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }

        [Test]
        public void WriteAndRead_RoundtripBytes()
        {
            var repository = new SaveRepository(tempRoot);
            var payload = Encoding.UTF8.GetBytes("payload-data");

            var metadata = repository.Write("slot1", payload);
            Assert.That(File.Exists(metadata.FullPath), Is.True);

            var readResult = repository.Read("slot1");
            CollectionAssert.AreEqual(payload, readResult.Data);
            Assert.That(readResult.Metadata.FullPath, Is.EqualTo(metadata.FullPath));
        }

        [Test]
        public void Read_MissingFileThrowsFileNotFound()
        {
            var repository = new SaveRepository(tempRoot);
            Assert.Throws<FileNotFoundException>(() => repository.Read("missing"));
        }

        [Test]
        public void Read_EmptyFileThrowsInvalidData()
        {
            var repository = new SaveRepository(tempRoot);
            var path = Path.Combine(tempRoot, "empty.json");
            File.WriteAllBytes(path, Array.Empty<byte>());

            Assert.Throws<InvalidDataException>(() => repository.Read("empty"));
        }

        [Test]
        public void Write_CreatesDirectoryAutomatically()
        {
            string nested = Path.Combine(tempRoot, "nested", "saves");
            var repository = new SaveRepository(nested);
            var payload = Encoding.UTF8.GetBytes("test");

            var metadata = repository.Write("custom", payload);
            Assert.That(File.Exists(metadata.FullPath), Is.True);
            Assert.That(Directory.Exists(Path.GetDirectoryName(metadata.FullPath) ?? string.Empty), Is.True);
        }
    }
}
