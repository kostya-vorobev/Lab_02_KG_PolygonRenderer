using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Linq;

namespace PolygonRenderer
{
    public partial class MainWindow : Window
    {
        private List<PolygonData> polygons = new List<PolygonData>(); // Список многоугольников
        private double[,] zBuffer; // Z-буфер для отслеживания глубины

        public MainWindow()
        {
            InitializeComponent(); // Инициализация компонентов окна
            zBuffer = new double[(int)Width, (int)Height]; // Создаем Z-буфер с размерами окна
            ClearZBuffer(); // Очищаем Z-буфер при старте
        }

        private void ClearZBuffer()
        {
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    zBuffer[i, j] = double.NegativeInfinity; // Устанавливаем значение для каждой ячейки
                }
            }
        }

        private string OpenFileDialog()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Select a Polygon File"
            };

            bool? result = openFileDialog.ShowDialog(); // Отображаем диалоговое окно
            return result == true ? openFileDialog.FileName : string.Empty;
        }

        private void LoadPolygonsFromFile(string fileName)
        {
            string[] lines = System.IO.File.ReadAllLines(fileName);
            Random rand = new Random();

            foreach (var line in lines)
            {
                string[] parts = line.Trim('{', '}').Split(';');
                List<Point> vertices = new List<Point>(); // Список вершин

                foreach (var part in parts)
                {
                    string[] coordinates = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (coordinates.Length == 3 && float.TryParse(coordinates[0], out float x) &&
                        float.TryParse(coordinates[1], out float y) &&
                        float.TryParse(coordinates[2], out float z))
                    {
                        vertices.Add(new Point(x, y, z)); // Создаём точку с глубиной z
                    }
                }

                if (vertices.Count > 0)
                {
                    PolygonData polygon = new PolygonData
                    {
                        Vertices = vertices,
                        Color = Color.FromRgb((byte)rand.Next(256), (byte)rand.Next(256), (byte)rand.Next(256))
                    };
                    polygons.Add(polygon);
                }
            }

            ActiveEdgesStatus.Text = $"Загружено многоугольников: {polygons.Count}";
        }

        private void LineScanAlgorithm(object sender, RoutedEventArgs e)
        {
            string fileName = OpenFileDialog();
            if (!string.IsNullOrEmpty(fileName))
            {
                LoadPolygonsFromFile(fileName);
                DrawingCanvas.Children.Clear();
                foreach (var polygon in polygons)
                {
                    DrawPolygonEdges(polygon);
                }
            }
        }

        private void DrawPolygonEdges(PolygonData polygon)
        {
            var vertices = polygon.Vertices;

            for (int i = 0; i < vertices.Count; i++)
            {
                Point p1 = vertices[i];
                Point p2 = vertices[(i + 1) % vertices.Count]; // Цикличность
                DrawEdge(p1, p2, p1.Z); // Используем Z-координату как глубину
            }
        }

        private void DrawEdge(Point p1, Point p2, float depth)
        {
            Brush highlightBrush = new SolidColorBrush(Colors.Red);
            int dx = (int)(p2.X - p1.X);
            int dy = (int)(p2.Y - p1.Y);
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            double xIncrement = dx / (double)steps;
            double yIncrement = dy / (double)steps;

            double x = p1.X;
            double y = p1.Y;

            for (int i = 0; i <= steps; i++)
            {
                int pixelX = (int)Math.Round(x);
                int pixelY = (int)Math.Round(y);

                if (pixelX >= 0 && pixelX < DrawingCanvas.ActualWidth && pixelY >= 0 && pixelY < DrawingCanvas.ActualHeight)
                {
                    Rectangle rect = new Rectangle
                    {
                        Width = 1,
                        Height = 1,
                        Fill = highlightBrush
                    };

                    Canvas.SetLeft(rect, pixelX);
                    Canvas.SetTop(rect, pixelY);
                    DrawingCanvas.Children.Add(rect);

                    if (depth > zBuffer[pixelX, pixelY])
                    {
                        zBuffer[pixelX, pixelY] = depth; // Обновляем значение в Z-буфере
                    }
                }

                x += xIncrement;
                y += yIncrement;
            }
        }

        private void FillButton_Click(object sender, RoutedEventArgs e)
        {
            DrawingCanvas.Children.Clear();
            ClearZBuffer(); // Очищаем Z-буфер для новой заливки

            // Сортируем многоугольники по глубине
            var sortedPolygons = polygons.OrderBy(p => p.Vertices.Average(v => v.Z)).ToList();

            foreach (var polygon in sortedPolygons)
            {
                DrawPolygon(polygon);
            }
        }

        private void DrawPolygon(PolygonData polygon)
        {
            var vertices = polygon.Vertices.ToList();

            if (vertices.Count == 3)
            {
                DrawTriangle(vertices.ToArray(), polygon);
            }
            else if (vertices.Count == 4)
            {
                DrawTriangle(new[] { vertices[0], vertices[1], vertices[2] }, polygon);
                DrawTriangle(new[] { vertices[0], vertices[2], vertices[3] }, polygon);
            }
            else
            {
                // Триангуляция многоугольников с 5 и более вершинами
                for (int i = 1; i < vertices.Count - 1; i++)
                {
                    DrawTriangle(new[] { vertices[0], vertices[i], vertices[i + 1] }, polygon);
                }
            }

            DrawCoordinates(polygon);
        }

        private void DrawTriangle(Point[] triangle, PolygonData polygon)
        {
            var vertices = triangle.OrderBy(v => v.Y).ToList();

            int minY = (int)Math.Ceiling(vertices[0].Y);
            int maxY = (int)Math.Floor(vertices[2].Y);

            for (int y = minY; y <= maxY; y++)
            {
                List<double> intersections = new List<double>();

                for (int i = 0; i < vertices.Count; i++)
                {
                    var p1 = vertices[i];
                    var p2 = vertices[(i + 1) % vertices.Count];

                    if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                    {
                        double x = p1.X + (y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                        intersections.Add(x);
                    }
                }

                intersections.Sort();
                for (int i = 0; i < intersections.Count; i += 2)
                {
                    int xStart = (int)Math.Ceiling(intersections[i]);
                    int xEnd = (int)Math.Floor(intersections[i + 1]);
                    for (int x = xStart; x <= xEnd; x++)
                    {
                        double z = GetDepthAtPoint(x, y, new PolygonData { Vertices = new List<Point> { triangle[0], triangle[1], triangle[2] } });
                        if (z > zBuffer[x, y])
                        {
                            zBuffer[x, y] = z;
                            DrawPixel(x, y, polygon.Color);
                        }
                    }
                }
            }
        }



        private void DrawCoordinates(PolygonData polygon)
        {
            foreach (var vertex in polygon.Vertices)
            {
                // Создаем текстовое поле для отображения координат
                TextBlock textBlock = new TextBlock
                {
                    Text = $"({vertex.X}, {vertex.Y}, {vertex.Z})",
                    Foreground = Brushes.Black,
                    FontWeight = FontWeights.Bold
                };

                // Устанавливаем позицию текста рядом с вершиной
                Canvas.SetLeft(textBlock, vertex.X + 5); // смещаем текст немного вправо
                Canvas.SetTop(textBlock, vertex.Y - 15); // смещаем текст немного вверх

                // Добавляем текст на канвас
                DrawingCanvas.Children.Add(textBlock);
            }
        }


        private double GetDepthAtPoint(int x, int y, PolygonData polygon)
        {
            // Используем барицентрические координаты для нахождения Z
            var vertices = polygon.Vertices;

            // Получаем координаты треугольника
            var p1 = vertices[0];
            var p2 = vertices[1];
            var p3 = vertices[2];

            double area = 0.5 * (-p2.Y * p3.X + p1.Y * (-p2.X + p3.X) + p1.X * (p2.Y - p3.Y) + p2.X * p3.Y);
            double s = 1 / (2 * area) * (p1.Y * p3.X - p1.X * p3.Y + (p3.Y - p1.Y) * x + (p1.X - p3.X) * y);
            double t = 1 / (2 * area) * (p1.X * p2.Y - p1.Y * p2.X + (p1.Y - p2.Y) * x + (p2.X - p1.X) * y);

            // Если точка находится внутри треугольника (s >= 0, t >= 0, s + t <= 1)
            if (s >= 0 && t >= 0 && (s + t) <= 1)
            {
                // Возвращаем Z-с-coordinate (глубину) по барицентрическим координатам
                return p1.Z * (1 - s - t) + p2.Z * s + p3.Z * t;
            }

            return double.NegativeInfinity; // вне треугольника
        }

        private void DrawPixel(int x, int y, Color color)
        {
            Rectangle rect = new Rectangle
            {
                Width = 1,
                Height = 1,
                Fill = new SolidColorBrush(color)
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            DrawingCanvas.Children.Add(rect);
        }


        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearZBuffer();
            polygons.Clear();
            DrawingCanvas.Children.Clear();
        }


    }

    public class PolygonData
    {
        public List<Point> Vertices { get; set; }
        public Color Color { get; set; }
    }

    public struct Point
    {
        public float X;
        public float Y;
        public float Z;

        public Point(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
