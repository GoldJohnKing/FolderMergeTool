using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Collections.Generic;
using Path = System.IO.Path;
using MessageBox = System.Windows.MessageBox;

namespace FolderMergeTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectBaseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BaseFolderTextBox.Text = dlg.SelectedPath;
            }
        }

        private void SelectFolderA_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FolderATextBox.Text = dlg.SelectedPath;
            }
        }

        private void SelectFolderB_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FolderBTextBox.Text = dlg.SelectedPath;
            }
        }

        private void MergeFolders_Click(object sender, RoutedEventArgs e)
        {
            string basePath = BaseFolderTextBox.Text.Trim();
            string aPath = FolderATextBox.Text.Trim();
            string bPath = FolderBTextBox.Text.Trim();
            ConflictFilesListBox.Items.Clear();
            SaveConflictListButtonState(false);
            if (!Directory.Exists(basePath) || !Directory.Exists(aPath) || !Directory.Exists(bPath))
            {
                MessageBox.Show("请确保所有文件夹路径均有效。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var dictBase = GetFileHashDict(basePath);
            var dictA = GetFileHashDict(aPath);
            var dictB = GetFileHashDict(bPath);

            var conflicts = GetConflictFiles(dictBase, dictA, dictB);
            if (conflicts.Count > 0)
            {
                foreach (var file in conflicts)
                {
                    ConflictFilesListBox.Items.Add(file);
                }
                SaveConflictListButtonState(true);
                MessageBox.Show("检测到冲突文件，无法合并。", "冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SaveConflictListButtonState(false);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string resultPath = Path.Combine(Directory.GetParent(basePath).FullName, $"Merged_{timestamp}");
            if (Directory.Exists(resultPath))
            {
                try { Directory.Delete(resultPath, true); } catch { }
            }
            Directory.CreateDirectory(resultPath);
            CopyAll(basePath, resultPath);
            var allKeys = new HashSet<string>(dictA.Keys);
            allKeys.UnionWith(dictB.Keys);
            foreach (var key in allKeys)
            {
                dictBase.TryGetValue(key, out var hashBase);
                dictA.TryGetValue(key, out var hashA);
                dictB.TryGetValue(key, out var hashB);
                string destFile = Path.Combine(resultPath, key);
                string srcFile = GetMergeSourceFile(aPath, bPath, key, hashBase, hashA, hashB);
                if (!string.IsNullOrEmpty(srcFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    File.Copy(srcFile, destFile, true);
                }
            }
            MessageBox.Show($"合并完成，结果文件夹：{resultPath}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 判断冲突文件列表
        /// </summary>
        private List<string> GetConflictFiles(Dictionary<string, string> dictBase, Dictionary<string, string> dictA, Dictionary<string, string> dictB)
        {
            var conflicts = new List<string>();
            var allKeys = new HashSet<string>(dictA.Keys);
            allKeys.IntersectWith(dictB.Keys);
            foreach (var key in allKeys)
            {
                dictBase.TryGetValue(key, out var hashBase);
                dictA.TryGetValue(key, out var hashA);
                dictB.TryGetValue(key, out var hashB);
                // 三者哈希均不同，判定为冲突
                if (!string.IsNullOrEmpty(hashBase) && !string.IsNullOrEmpty(hashA) && !string.IsNullOrEmpty(hashB)
                    && hashA != hashBase && hashB != hashBase && hashA != hashB)
                {
                    conflicts.Add(key);
                }
            }
            return conflicts;
        }

        /// <summary>
        /// 根据哈希关系决定合并时应拷贝哪个文件，若返回空则保留Base文件
        /// </summary>
        private string GetMergeSourceFile(string aPath, string bPath, string key, string hashBase, string hashA, string hashB)
        {
            // A与Base相同，B不同，拷贝B
            if (!string.IsNullOrEmpty(hashBase) && hashA == hashBase && !string.IsNullOrEmpty(hashB) && hashB != hashBase)
            {
                return Path.Combine(bPath, key);
            }
            // B与Base相同，A不同，拷贝A
            else if (!string.IsNullOrEmpty(hashBase) && hashB == hashBase && !string.IsNullOrEmpty(hashA) && hashA != hashBase)
            {
                return Path.Combine(aPath, key);
            }
            // A与B相同且与Base不同，拷贝A（也可以拷贝B）
            else if (!string.IsNullOrEmpty(hashA) && !string.IsNullOrEmpty(hashB) && hashA == hashB && (string.IsNullOrEmpty(hashBase) || hashA != hashBase))
            {
                return Path.Combine(aPath, key);
            }
            // 仅A存在且与Base不同，拷贝A
            else if (!string.IsNullOrEmpty(hashA) && (string.IsNullOrEmpty(hashBase) || hashA != hashBase) && string.IsNullOrEmpty(hashB))
            {
                return Path.Combine(aPath, key);
            }
            // 仅B存在且与Base不同，拷贝B
            else if (!string.IsNullOrEmpty(hashB) && (string.IsNullOrEmpty(hashBase) || hashB != hashBase) && string.IsNullOrEmpty(hashA))
            {
                return Path.Combine(bPath, key);
            }
            // 其他情况（如三者哈希均不同或都相同），返回空，保留Base
            else
            {
                return null;
            }
        }

        private void SaveConflictListButtonState(bool enabled)
        {
            if (this.FindName("SaveConflictListButton") is System.Windows.Controls.Button btn)
            {
                btn.IsEnabled = enabled;
            }
        }

        private Dictionary<string, string> GetFileHashDict(string folder)
        {
            var dict = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(folder, file);
                dict[relPath] = GetFileSha256(file);
            }
            return dict;
        }

        private string GetFileSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void CopyAll(string sourceDir, string targetDir)
        {
            foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relDir = Path.GetRelativePath(sourceDir, dir);
                Directory.CreateDirectory(Path.Combine(targetDir, relDir));
            }
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relFile = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(targetDir, relFile);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(file, destFile, true);
            }
        }

        private void SaveConflictList_Click(object sender, RoutedEventArgs e)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "文本文件 (*.txt)|*.txt";
            dlg.FileName = $"Conflicts_{timestamp}.txt";
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    using (var sw = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8))
                    {
                        foreach (var item in ConflictFilesListBox.Items)
                        {
                            sw.WriteLine(item.ToString());
                        }
                    }
                    MessageBox.Show("冲突文件列表已保存。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}