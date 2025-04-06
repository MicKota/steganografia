using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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

                    FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap();
                    convertedBitmap.BeginInit();
                    convertedBitmap.Source = bitmapImage;
                    convertedBitmap.DestinationFormat = PixelFormats.Bgr24;
                    convertedBitmap.EndInit();

                    bitmap = new WriteableBitmap(convertedBitmap);
                    imagePreview.Source = bitmap;

                    int availableBits = bitmap.PixelWidth * bitmap.PixelHeight * 3;
                    MessageBox.Show($"Maksymalna pojemność nośnika: {availableBits / 8} bajtów ({availableBits} bitów)",
                                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd ładowania obrazu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Tekst został załadowany!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd odczytu pliku: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Encode_Click(object sender, RoutedEventArgs e)
        {
            if (bitmap == null || string.IsNullOrEmpty(hiddenText))
            {
                MessageBox.Show("Najpierw załaduj obraz i tekst!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(hiddenText + "\0");
                int requiredBits = textBytes.Length * 8;
                int availableBits = bitmap.PixelWidth * bitmap.PixelHeight * 3;

                if (requiredBits > availableBits)
                {
                    MessageBox.Show($"Obraz jest za mały! Wymagane bity: {requiredBits}, Dostępne: {availableBits}",
                                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int stride = bitmap.PixelWidth * 3;
                byte[] pixelData = new byte[bitmap.PixelHeight * stride];
                bitmap.CopyPixels(pixelData, stride, 0);

                int bitIndex = 0;
                for (int i = 0; i < pixelData.Length; i++)
                {
                    if (bitIndex >= requiredBits) break;
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
                    bitmap = encodedBitmap;
                    imagePreviewModified.Source = encodedBitmap;

                    int usedBytes = requiredBits / 8;
                    double usagePercent = (double)usedBytes / (availableBits / 8) * 100;
                    MessageBox.Show($"Zajętość nośnika: {usedBytes} bajtów ({requiredBits} bitów)\n" +
                                    $"Pozostałe miejsce: {(availableBits - requiredBits) / 8} bajtów\n" +
                                    $"Wykorzystano: {usagePercent:F2}%",
                                    "Zajętość nośnika", MessageBoxButton.OK, MessageBoxImage.Information);

                    DrawCapacityChart(usagePercent); // Rysowanie wykresu
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kodowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Decode_Click(object sender, RoutedEventArgs e)
        {
            if (bitmap == null)
            {
                MessageBox.Show("Najpierw załaduj obraz!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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

                string extractedText = System.Text.Encoding.UTF8.GetString(resultBytes).Split('\0')[0];
                MessageBox.Show($"Odczytany tekst:\n{extractedText}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dekodowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DrawCapacityChart(double usagePercent)
        {
                // Wyczyść poprzedni rysunek
                capacityChart.Children.Clear();

                double centerX = capacityChart.ActualWidth / 2;
                double centerY = capacityChart.ActualHeight / 2;
                double radius = Math.Min(capacityChart.ActualWidth, capacityChart.ActualHeight) / 2 - 10;

                // Rysowanie okręgu tła
                Ellipse backgroundCircle = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = Brushes.LightGray,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(backgroundCircle, centerX - radius);
                Canvas.SetTop(backgroundCircle, centerY - radius);
                capacityChart.Children.Add(backgroundCircle);

                // Konwersja procentowej zajętości na kąt
                double angle = (usagePercent / 100) * 360;
                double radians = angle * Math.PI / 180;

                // Punkt końcowy łuku
                double endX = centerX + radius * Math.Cos(radians - Math.PI / 2);
                double endY = centerY + radius * Math.Sin(radians - Math.PI / 2);

                // Tworzenie ścieżki wykresu
                PathFigure pathFigure = new PathFigure { StartPoint = new Point(centerX, centerY - radius) };

                // Łuk wykresu
                ArcSegment arcSegment = new ArcSegment
                {
                    Point = new Point(endX, endY),
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = angle > 180
                };

                // Linia powrotna do środka (zamyka figurę)
                LineSegment lineSegment1 = new LineSegment(new Point(centerX, centerY), true);
                LineSegment lineSegment2 = new LineSegment(new Point(centerX, centerY - radius), true);

                // Dodanie segmentów do ścieżki
                pathFigure.Segments.Add(arcSegment);
                pathFigure.Segments.Add(lineSegment1);
                pathFigure.Segments.Add(lineSegment2);

                // Tworzenie geometrii ścieżki
                PathGeometry pathGeometry = new PathGeometry();
                pathGeometry.Figures.Add(pathFigure);

                // Tworzenie kształtu
                System.Windows.Shapes.Path pathShape = new System.Windows.Shapes.Path
                {
                    Fill = new SolidColorBrush(Color.FromRgb(50, 150, 255)), // Niebieskie wypełnienie
                    Stroke = Brushes.DarkBlue,
                    StrokeThickness = 1,
                    Data = pathGeometry
                };

                // Dodanie do kontenera
                capacityChart.Children.Add(pathShape);

                // Dodanie etykiety z procentem
                TextBlock percentLabel = new TextBlock
                {
                    Text = $"{usagePercent:F1}%",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(percentLabel, centerX - 25);
                Canvas.SetTop(percentLabel, centerY - 10);
                capacityChart.Children.Add(percentLabel);
            }

        private void CheckExternalImage_Click(object sender, RoutedEventArgs e)
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

                    FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap();
                    convertedBitmap.BeginInit();
                    convertedBitmap.Source = bitmapImage;
                    convertedBitmap.DestinationFormat = PixelFormats.Bgr24;
                    convertedBitmap.EndInit();

                    WriteableBitmap externalBitmap = new WriteableBitmap(convertedBitmap);
                    imagePreview.Source = externalBitmap;

                    // Sprawdzenie, czy obraz zawiera ukryty tekst
                    int stride = externalBitmap.PixelWidth * 3;
                    byte[] pixelData = new byte[externalBitmap.PixelHeight * stride];
                    externalBitmap.CopyPixels(pixelData, stride, 0);

                    // Próba odczytania ukrytego tekstu
                    byte[] resultBytes = new byte[pixelData.Length / 8];
                    int bitIndex = 0;

                    for (int i = 0; i < pixelData.Length && bitIndex < resultBytes.Length * 8; i++)
                    {
                        byte bit = (byte)(pixelData[i] & 1);
                        resultBytes[bitIndex / 8] |= (byte)(bit << (7 - (bitIndex % 8)));
                        bitIndex++;
                    }

                    try
                    {
                        string extractedText = System.Text.Encoding.UTF8.GetString(resultBytes);

                        // Sprawdzenie, czy odczytany tekst zawiera znaki null (koniec wiadomości)
                        if (extractedText.Contains('\0'))
                        {
                            extractedText = extractedText.Split('\0')[0];

                            // Sprawdzenie, czy tekst zawiera czytelne znaki
                            bool containsReadableText = !string.IsNullOrWhiteSpace(extractedText) &&
                                                      extractedText.Any(c => char.IsLetterOrDigit(c));

                            if (containsReadableText)
                            {
                                MessageBox.Show($"Znaleziono ukryty tekst w obrazie:\n{extractedText}",
                                                "Wykryto ukrytą wiadomość",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show("Nie wykryto czytelnego ukrytego tekstu w obrazie.",
                                                "Brak ukrytej wiadomości",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("Nie wykryto ukrytego tekstu w obrazie.",
                                            "Brak ukrytej wiadomości",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Information);
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Nie wykryto ukrytego tekstu w obrazie lub format tekstu jest nieobsługiwany.",
                                        "Brak ukrytej wiadomości",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd ładowania obrazu: {ex.Message}",
                                    "Błąd",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
        }

    }

}
