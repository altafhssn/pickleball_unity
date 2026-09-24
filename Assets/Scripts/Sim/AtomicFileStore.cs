using System.IO;
using System.Text;

namespace Pickleball.Sim
{
    public static class AtomicFileStore
    {
        /// <summary>Commit a fully flushed replacement and retain the previous successful save.</summary>
        public static void Write(string path, string contents)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
