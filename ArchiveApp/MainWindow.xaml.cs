using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ArchiveApp
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> _files = new();

        public MainWindow()
        {
            InitializeComponent();
            FilesList.ItemsSource = _files;
            UpdateStatus("أضف الملفات ثم اضغط على إنشاء الأرشيف.");
        }

        private void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "اختر الملفات للأرشفة"
            };

            if (dialog.ShowDialog() == true)
            {
                var addedCount = 0;
                foreach (var file in dialog.FileNames)
                {
                    if (!_files.Contains(file))
                    {
                        _files.Add(file);
                        addedCount++;
                    }
                }

                UpdateStatus(addedCount > 0 ? $"تمت إضافة {addedCount} ملف/ملفات." : "كل الملفات موجودة بالفعل في القائمة.");
            }
        }

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = FilesList.SelectedItems.Cast<string>().ToList();
            foreach (var file in selectedItems)
            {
                _files.Remove(file);
            }

            UpdateStatus(selectedItems.Count > 0 ? "تم حذف العناصر المحددة." : "لم يتم تحديد أي عنصر للحذف.");
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _files.Clear();
            UpdateStatus("تم مسح القائمة.");
        }

        private void PickDestinationButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "ملف مضغوط (.zip)|*.zip",
                DefaultExt = "zip",
                AddExtension = true,
                Title = "تحديد مكان حفظ الأرشيف"
            };

            if (dialog.ShowDialog() == true)
            {
                DestinationPathBox.Text = dialog.FileName;
            }
        }

        private void ArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_files.Count == 0)
            {
                MessageBox.Show("الرجاء إضافة ملفات قبل إنشاء الأرشيف.", "لا توجد ملفات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var destinationPath = DestinationPathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                MessageBox.Show("الرجاء اختيار مسار الحفظ.", "مسار غير صالح", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                MessageBox.Show("المجلد المحدد غير موجود.", "مسار غير صالح", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var normalizedSources = _files
                .Select(path => new
                {
                    Original = path,
                    Full = Path.GetFullPath(path)
                })
                .ToList();

            var destinationFullPath = Path.GetFullPath(destinationPath);
            if (normalizedSources.Any(file => string.Equals(file.Full, destinationFullPath, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("مسار الحفظ يطابق أحد الملفات المحددة للأرشفة. الرجاء اختيار اسم مختلف.", "مسار غير صالح", MessageBoxButton.OK, MessageBoxImage.Warning);
                UpdateStatus("تم إيقاف إنشاء الأرشيف لأن مسار الحفظ يطابق ملفاً محدداً.");
                return;
            }

            try
            {
                if (File.Exists(destinationFullPath))
                {
                    File.Delete(destinationFullPath);
                }

                var compressionLevel = ResolveCompressionLevel();
                using var archive = ZipFile.Open(destinationFullPath, ZipArchiveMode.Create);

                var duplicates = new Dictionary<string, int>();
                foreach (var file in normalizedSources)
                {
                    if (!File.Exists(file.Full))
                    {
                        continue;
                    }

                    var entryName = Path.GetFileName(file.Original);
                    if (duplicates.TryGetValue(entryName, out var counter))
                    {
                        counter++;
                        duplicates[entryName] = counter;
                        entryName = $"{Path.GetFileNameWithoutExtension(file.Original)}_{counter}{Path.GetExtension(file.Original)}";
                    }
                    else
                    {
                        duplicates[entryName] = 0;
                    }

                    archive.CreateEntryFromFile(file.Full, entryName, compressionLevel);
                }

                UpdateStatus($"تم إنشاء الأرشيف بنجاح في: {destinationFullPath}");
                MessageBox.Show("تم إنشاء الأرشيف بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (IOException ioEx)
            {
                MessageBox.Show($"تعذر إنشاء الأرشيف: {ioEx.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("فشل إنشاء الأرشيف بسبب خطأ في الملفات أو الأقراص.");
            }
            catch (UnauthorizedAccessException accessEx)
            {
                MessageBox.Show($"ليست لديك صلاحية الكتابة في المسار المحدد: {accessEx.Message}", "صلاحيات غير كافية", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("فشل إنشاء الأرشيف بسبب صلاحيات غير كافية.");
            }
        }

        private CompressionLevel ResolveCompressionLevel()
        {
            if (CompressionLevelBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                return tag switch
                {
                    "None" => CompressionLevel.NoCompression,
                    "Fastest" => CompressionLevel.Fastest,
                    _ => CompressionLevel.Optimal
                };
            }

            return CompressionLevel.Optimal;
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }
    }
}
