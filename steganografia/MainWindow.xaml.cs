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
                Filter = "Bitmap Images (*.bmp)|*.bmp"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                BitmapImage bitmapImage = new BitmapImage(new Uri(openFileDialog.FileName));
                bitmap = new WriteableBitmap(bitmapImage);

                MessageBox.Show($"Format obrazu: {bitmap.Format}", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);

                // Jeśli obraz nie jest w formacie Bgr24, przekonwertuj go
                if (bitmap.Format != PixelFormats.Bgr24)
                {
                    MessageBox.Show("Obraz nie jest w formacie 24-bitowym, konwertowanie na 24-bitowy BMP...", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    bitmap = ConvertToBgr24(bitmapImage);
                }

                imagePreview.Source = bitmap;
            }
        }

        private WriteableBitmap ConvertToBgr24(BitmapImage originalImage)
        {
            int width = originalImage.PixelWidth;
            int height = originalImage.PixelHeight;
            int stride = width * 3; // 3 bytes per pixel (24 bits)

            // Obliczamy odpowiednią wielkość tablicy pixelData
            byte[] pixelData = new byte[height * stride];

            // Sprawdzamy czy obraz ma poprawne wymiary i inicjalizujemy tablicę pixelData
            if (pixelData.Length > 0)
            {
                // Wczytanie danych pikseli
                originalImage.CopyPixels(pixelData, stride, 0);
            }
            else
            {
                MessageBox.Show("Obraz jest za mały do konwersji.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            // Utworzenie nowego WriteableBitmap w formacie Bgr24
            WriteableBitmap convertedBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
            convertedBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixelData, stride, 0);

            return convertedBitmap;
        }


        private void LoadText_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                hiddenText = File.ReadAllText(openFileDialog.FileName);
                MessageBox.Show("Tekst został załadowany!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Encode_Click(object sender, RoutedEventArgs e)
        {
            if (bitmap == null || string.IsNullOrEmpty(hiddenText))
            {
                MessageBox.Show("Załaduj obraz i tekst przed kodowaniem!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(hiddenText + "\0");
            int capacity = bitmap.PixelWidth * bitmap.PixelHeight * 3;
            if (textBytes.Length * 8 > capacity)
            {
                MessageBox.Show("Tekst jest za duży, aby zmieścić go w tym obrazie!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int index = 0;
            int stride = bitmap.PixelWidth * 3;
            byte[] pixelData = new byte[bitmap.PixelHeight * stride];
            bitmap.CopyPixels(pixelData, stride, 0);

            for (int i = 0; i < pixelData.Length; i++)
            {
                if (index >= textBytes.Length * 8) break;
                pixelData[i] = (byte)((pixelData[i] & 0xFE) | ((textBytes[index / 8] >> (7 - (index % 8))) & 1));
                index++;
            }

            WriteableBitmap encodedBitmap = new WriteableBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Bgr24, null);
            encodedBitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), pixelData, stride, 0);

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Bitmap Images (*.bmp)|*.bmp"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                using (FileStream stream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                {
                    BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(encodedBitmap));
                    encoder.Save(stream);
                }
                MessageBox.Show("Tekst został ukryty w obrazie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                imagePreviewModified.Source = encodedBitmap;
            }
        }

        private void Decode_Click(object sender, RoutedEventArgs e)
        {
            if (bitmap == null)
            {
                MessageBox.Show("Załaduj obraz przed dekodowaniem!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int stride = bitmap.PixelWidth * 3;
            byte[] pixelData = new byte[bitmap.PixelHeight * stride];
            bitmap.CopyPixels(pixelData, stride, 0);

            byte[] textBytes = new byte[pixelData.Length / 8];
            int index = 0;

            for (int i = 0; i < pixelData.Length; i++)
            {
                if (index >= textBytes.Length * 8) break;
                textBytes[index / 8] |= (byte)((pixelData[i] & 1) << (7 - (index % 8)));
                index++;
            }

            string extractedText = System.Text.Encoding.UTF8.GetString(textBytes).Split('\0')[0];
            MessageBox.Show($"Odczytany tekst: {extractedText}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
