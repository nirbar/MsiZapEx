using System;
using System.Collections.Generic;
using System.IO;

namespace MsiZapEx
{
    class FileSystemModifier : IDisposable
    {
        private List<string> _deleteFolders = new List<string>();
        private List<string> _deleteFiles = new List<string>();

        public void Dispose()
        {
            foreach (string folder in _deleteFolders)
            {
                DeleteFolder(folder);
            }
            _deleteFolders.Clear();
            foreach (string file in _deleteFiles)
            {
                DeleteFile(file);
            }
            _deleteFiles.Clear();
        }

        private void DeleteFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            if ((Settings.Instance?.DryRun == true) || (Settings.Instance?.Verbose == true))
            {
                Console.WriteLine($"Delete file '{path}'");
                if (Settings.Instance?.DryRun == true)
                {
                    return;
                }
            }

            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }

        private void DeleteFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            if ((Settings.Instance?.DryRun == true) || (Settings.Instance?.Verbose == true))
            {
                Console.WriteLine($"Delete folder '{path}'");
                if (Settings.Instance?.DryRun == true)
                {
                    return;
                }
            }

            IEnumerable<string> files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                DeleteFile(file);
            }
            Directory.Delete(path, true);
        }

        public void DeferDeleteFile(string path)
        {
            _deleteFiles.Add(path);
        }

        public void DeferDeleteFolder(string path)
        {
            _deleteFolders.Add(path);
        }
    }
}
