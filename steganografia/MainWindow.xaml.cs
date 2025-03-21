using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace SteganografiaWPF
{
    public partial class MainWindow : Window
    {
        private WriteableBitmap bitmap;
        private string hiddenText;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Obrazy (*.bmp;*.jpg;*.png)|*.bmp;*.jpg;*.png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(openFileDialog.FileName);
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    // Konwersja do formatu 24-bitowego BMP
                    FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap();
                    convertedBitmap.BeginInit();
                    convertedBitmap.Source = bitmapImage;
                    convertedBitmap.DestinationFormat = PixelFormats.Bgr24;
                    convertedBitmap.EndInit();

                    bitmap = new WriteableBitmap(convertedBitmap);
                    imagePreview.Source = bitmap;

                    MessageBox.Show("Obraz przekonwertowany do formatu 24-bitowego BMP!",
                                  "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd ładowania obrazu: {ex.Message}",
                                  "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    bitmap = null;
                }
            }
        }

        private void LoadText_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Pliki tekstowe (*.txt)|*.txt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    hiddenText = File.ReadAllText(openFileDialog.FileName);
                    MessageBox.Show("Tekst został załadowany!",
                                  "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd odczytu pliku: {ex.Message}",
                                  "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Encode_Click(object sender, RoutedEventArgs e)
        {
            if (bitmap == null || string.IsNullOrEmpty(hiddenText))
            {
                MessageBox.Show("Najpierw załaduj obraz i tekst!",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(hiddenText + "\0");
                int requiredPixels = (int)Math.Ceiling(textBytes.Length * 8 / 3.0);
                int availablePixels = bitmap.PixelWidth * bitmap.PixelHeight;

                if (requiredPixels > availablePixels)
                {
                    MessageBox.Show($"Obraz jest za mały! Wymagane piksele: {requiredPixels}, Dostępne: {availablePixels}",
                                  "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int stride = bitmap.PixelWidth * 3;
                byte[] pixelData = new byte[bitmap.PixelHeight * stride];
                bitmap.CopyPixels(pixelData, stride, 0);

                int bitIndex = 0;
                for (int i = 0; i < pixelData.Length; i++)
                {
                    if (bitIndex >= textBytes.Length * 8) break;

                    byte textBit = (byte)((textBytes[bitIndex / 8] >> (7 - (bitIndex % 8))) & 1);
                    pixelData[i] = (byte)((pixelData[i] & 0xFE) | textBit);
                    bitIndex++;
                }

                WriteableBitmap encodedBitmap = new WriteableBitmap(
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    96, 96,
                    PixelFormats.Bgr24,
                    null);

                encodedBitmap.WritePixels(
                    new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight),
                    pixelData,
                    stride,
                    0);

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "Obraz BMP (*.bmp)|*.bmp"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (FileStream stream = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(encodedBitmap));
                        encoder.Save(stream);
                    }
                    imagePreviewModified.Source = encodedBitmap;
                    MessageBox.Show("Tekst został zakodowany w obrazie!",
                                  "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kodowania: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Decode_Click(object sender, RoutedEventArgs e)
        {
            if (bitmap == null)
            {
                MessageBox.Show("Najpierw załaduj obraz!",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                int stride = bitmap.PixelWidth * 3;
                byte[] pixelData = new byte[bitmap.PixelHeight * stride];
                bitmap.CopyPixels(pixelData, stride, 0);

                byte[] resultBytes = new byte[pixelData.Length / 8];
                int bitIndex = 0;

                for (int i = 0; i < pixelData.Length; i++)
                {
                    if (bitIndex >= resultBytes.Length * 8) break;

                    byte bit = (byte)(pixelData[i] & 1);
                    resultBytes[bitIndex / 8] |= (byte)(bit << (7 - (bitIndex % 8)));
                    bitIndex++;
                }

                string extractedText = System.Text.Encoding.UTF8.GetString(resultBytes)
                    .Split('\0')[0];

                MessageBox.Show($"Odczytany tekst:\n{extractedText}",
                              "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dekodowania: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
